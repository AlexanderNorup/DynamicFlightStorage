using DynamicFlightStorageDTOs;
using Neo4j.Driver;
using System.Text;
using Testcontainers.Neo4j;

namespace Neo4jDataStore
{
    public class TimeBucketedNeo4jDataStore : IEventDataStore, IDisposable
    {
        public static readonly TimeSpan TimeBucketSize = TimeSpan.FromHours(1);

        private Neo4jContainer? _container;
        private IDriver? _database;
        private static readonly QueryConfig QueryConfig = new QueryConfig(database: "neo4j");
        private readonly IWeatherService _weatherService;
        private readonly IRecalculateFlightEventPublisher _flightRecalculation;
        public TimeBucketedNeo4jDataStore(IWeatherService weatherService, IRecalculateFlightEventPublisher recalculateFlightEventPublisher)
        {
            _weatherService = weatherService ?? throw new ArgumentNullException(nameof(weatherService));
            _flightRecalculation = recalculateFlightEventPublisher ?? throw new ArgumentNullException(nameof(recalculateFlightEventPublisher));
        }

        public async Task StartAsync()
        {
            _container = new Neo4jBuilder()
#if DEBUG
                .WithExposedPort(7474)
#endif
                .Build();
            await _container.StartAsync();

            var driver = GraphDatabase.Driver(_container.GetConnectionString());
            await driver.VerifyConnectivityAsync();

            var initPath = Path.Combine(AppContext.BaseDirectory, "DatabaseInit", "timeBucketedInit.cypher");
            foreach (var line in await File.ReadAllLinesAsync(initPath).ConfigureAwait(false))
            {
                if (line.Trim().StartsWith("//"))
                {
                    continue;
                }

                await driver.ExecutableQuery(line)
                    .WithConfig(QueryConfig)
                    .ExecuteAsync()
                    .ConfigureAwait(false);
            }

            _database = driver;

#if DEBUG
            Console.WriteLine($"Neo4j Time-Bucketed ConnectionString: {_container.GetConnectionString()}.\n" +
                $"Web-interface: http://localhost:{_container.GetMappedPublicPort(7474)}/browser?dbms={_container.GetConnectionString()}");
#endif
        }

        public async Task ResetAsync()
        {
            if (_database is not null)
            {
                await _database.DisposeAsync().AsTask();
            }
            if (_container is not null)
            {
                await _container.DisposeAsync().AsTask();
            }
            await StartAsync();
        }

        public async Task AddOrUpdateFlightAsync(Flight flight)
        {
            if (_database is null)
            {
                throw new InvalidOperationException("Database is not ready");
            }

            await using var session = _database.AsyncSession();
            // Executes it as a single transaction
            await session.ExecuteWriteAsync(async tx =>
            {
                await CommonGraphDatabaseCommands.CreateOrUpdateFlight(tx, flight.FlightIdentification);

                var weather = _weatherService.GetWeatherCategoriesForFlight(flight);

                foreach (var airport in flight.GetAllAirports().Distinct())
                {
                    await CreateOrUpdateTimeBucketRelation(tx, flight.FlightIdentification, airport, weather.GetValueOrDefault(airport, WeatherCategory.Undefined),
                        flight.ScheduledTimeOfDeparture, flight.ScheduledTimeOfArrival);
                }
            });
        }

        private async Task CreateOrUpdateTimeBucketRelation(IAsyncQueryRunner tx,
            string flightId, string icao, WeatherCategory weatherCategory, DateTime departure, DateTime arrival)
        {
            // We should make a time-bucket instance for every time-slot the flight occupies.
            const string TimeBucketCreationQuery1 =
                """
                MERGE (a:Airport {icao: $icao})
                    WITH a
                MERGE (a) -[h:HasFlightIn]-> (t:TimeBucket {
                    icao: $icao, 
                    timeSliceStart: 
                """;

            const string TimeBucketCreationQuery2 =
                    """
                })
                    WITH t
                MATCH (f:Flight {id: $flightId})
                MERGE (t) -[e:USES]-> (f)
                SET e.weather = $weatherLevel, e.dep = $departure, e.arr = $arrival
                """;

            var parameters = new Dictionary<string, object>
            {
                { "icao", icao},
                { "flightId", flightId},
                { "weatherLevel", (int)weatherCategory},
                { "departure", ((DateTimeOffset)departure).ToUnixTimeSeconds()},
                { "arrival", ((DateTimeOffset)arrival).ToUnixTimeSeconds()}
            };

            var timeBucketsToCreate = new List<long>();
            var current = GetTimeSlot(departure);
            var arrivalTimeSlot = GetTimeSlot(arrival);
            while (current <= arrivalTimeSlot)
            {
                timeBucketsToCreate.Add(((DateTimeOffset)current).ToUnixTimeSeconds());
                current = current.Add(TimeBucketSize);
            }

            var query = new StringBuilder();
            int i = 0;
            foreach (var timeSlice in timeBucketsToCreate)
            {
                var key = $"timeSliceStart{i++}";
                parameters.Add(key, timeSlice);
                query.Append(TimeBucketCreationQuery1);
                query.Append("$");
                query.Append(key);
                query.Append(TimeBucketCreationQuery2);

#if DEBUG
                query.Append(", ");
                query.Append("e.humanDep = \"").Append(departure.ToString()).Append("\", ");
                query.Append("e.humanArr = \"").Append(arrival.ToString()).Append("\", ");
                query.Append("t.humanTimeSlice = \"").Append(DateTimeOffset.FromUnixTimeSeconds(timeSlice).UtcDateTime.ToString()).AppendLine("\" ");
#endif 
                query.AppendLine(); // Add an empty line between queries
            }

            await tx.RunAsync(query.ToString(), parameters).ConfigureAwait(false);
        }

        private static DateTime GetTimeSlot(DateTime dateTime)
        {
            var ticksLeftOver = dateTime.Ticks % TimeBucketSize.Ticks;
            var roundedDown = dateTime.Subtract(TimeSpan.FromTicks(ticksLeftOver));
            return roundedDown;
        }

        public async Task AddWeatherAsync(Weather weather)
        {
            if (_database is null)
            {
                throw new InvalidOperationException("Database is not ready");
            }

            await using var session = _database.AsyncSession();

            const string SearchQuery =
                   """
                    MATCH 
                        (t:TimeBucket {icao: $icao} WHERE t.timeSliceStart >= $validFromBucket and t.timeSliceStart <= $validToBucket) 
                        -[u:USES WHERE 
                            u.weather < $weatherLevel
                            and u.dep <= $validTo
                            and $validFrom <= u.arr 
                        ]-> 
                        (f:Flight {recalculating: false}) 
                    RETURN DISTINCT f.id
                    """;

            var result = await session.RunAsync(SearchQuery, new
            {
                icao = weather.Airport,
                validFromBucket = ((DateTimeOffset)GetTimeSlot(weather.ValidFrom)).ToUnixTimeSeconds(),
                validToBucket = ((DateTimeOffset)GetTimeSlot(weather.ValidTo)).ToUnixTimeSeconds(),
                weatherLevel = (int)weather.WeatherLevel,
                validFrom = ((DateTimeOffset)weather.ValidFrom).ToUnixTimeSeconds(),
                validTo = ((DateTimeOffset)weather.ValidTo).ToUnixTimeSeconds(),
            }).ConfigureAwait(false);

            // Loop through the records asynchronously
            if ((await result.PeekAsync().ConfigureAwait(false)) is not null)
            {
                var records = await result.ToListAsync().ConfigureAwait(false);

                // Executes it as a single transaction
                await session.ExecuteWriteAsync(async tx =>
                {
                    var recalculatedFlights = new string[records.Count];
                    int i = 0;
                    foreach (var record in records)
                    {
                        // Each current read in buffer can be reached via Current
                        var fetched = record[0] as string;
                        if (string.IsNullOrEmpty(fetched))
                        {
                            // Should not happen, but to be safe anyway
                            Console.WriteLine("Flight ID was null or empty or not parse-able as a string: " + record[0]);
                            continue;
                        }

                        await _flightRecalculation.PublishRecalculationAsync(fetched).ConfigureAwait(false);
                        recalculatedFlights[i++] = fetched;
                    }

                    if (records.Count > 0)
                    {
                        await CommonGraphDatabaseCommands.SetRecalculation(tx, recalculatedFlights.ToArray());
                    }
                });
            }
        }

        public async Task DeleteFlightAsync(string id)
        {
            if (_database is null)
            {
                throw new InvalidOperationException("Database is not ready");
            }

            const string Query =
                """
                MATCH(n:Flight { name: $flightId})
                DETACH DELETE n
                """;

            await using var session = _database.AsyncSession();
            await session.RunAsync(new Query(Query, new { flightId = id }));
        }

        public void Dispose()
        {
            if (_database is not null)
            {
                _database.DisposeAsync().AsTask().GetAwaiter().GetResult();
                _database = null;
            }
            if (_container is not null)
            {
                _container.DisposeAsync().AsTask().GetAwaiter().GetResult();
                _container = null;
            }
        }
    }
}
