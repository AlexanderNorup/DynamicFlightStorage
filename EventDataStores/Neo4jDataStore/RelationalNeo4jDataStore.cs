using DynamicFlightStorageDTOs;
using Neo4j.Driver;
using Testcontainers.Neo4j;

namespace Neo4jDataStore
{
    public class RelationalNeo4jDataStore : IEventDataStore, IDisposable
    {
        private Neo4jContainer? _container;
        private IDriver? _database;
        private static readonly QueryConfig QueryConfig = new QueryConfig(database: "neo4j");
        private readonly IWeatherService _weatherService;
        private readonly IRecalculateFlightEventPublisher _flightRecalculation;
        public RelationalNeo4jDataStore(IWeatherService weatherService, IRecalculateFlightEventPublisher recalculateFlightEventPublisher)
        {
            _weatherService = weatherService ?? throw new ArgumentNullException(nameof(weatherService));
            _flightRecalculation = recalculateFlightEventPublisher ?? throw new ArgumentNullException(nameof(recalculateFlightEventPublisher));
        }

        public async Task StartAsync()
        {
            var builder = new Neo4jBuilder();
#if DEBUG
            builder = builder.WithExposedPort(7474);
#endif
            if (Environment.GetEnvironmentVariable("ENABLE_TEMPFS") is not null)
            {
                builder = builder.WithTmpfsMount("/data");
            }

            _container = builder.Build();
            await _container.StartAsync();

            var driver = GraphDatabase.Driver(_container.GetConnectionString());
            await driver.VerifyConnectivityAsync();

            var initPath = Path.Combine(AppContext.BaseDirectory, "DatabaseInit", "relationalInit.cypher");
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
            Console.WriteLine($"Neo4j Relational ConnectionString: {_container.GetConnectionString()}.\n" +
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
                    // Insert the airport
                    //await CommonGraphDatabaseCommands.CreateOrUpdateAirport(tx, airport);
                    await CreateOrUpdateRelation(tx, flight.FlightIdentification, airport, weather.GetValueOrDefault(airport, WeatherCategory.Undefined),
                        flight.ScheduledTimeOfDeparture, flight.ScheduledTimeOfArrival);
                }
            });
        }

        private async Task CreateOrUpdateRelation(IAsyncQueryRunner tx,
            string flightId, string icao, WeatherCategory weatherCategory, DateTimeOffset departure, DateTimeOffset arrival)
        {
            const string Query =
                """
                MERGE (a:Airport {icao: $icao})
                    WITH a
                MATCH (f:Flight {id: $flightId})
                MERGE (a) -[e:USES]-> (f)
                SET e.weather = $weatherLevel, e.dep = $departure, e.arr = $arrival;
                """;

            await tx.RunAsync(Query, new
            {
                flightId,
                icao,
                weatherLevel = (int)weatherCategory,
                departure = departure.ToUnixTimeSeconds(),
                arrival = arrival.ToUnixTimeSeconds()
            }).ConfigureAwait(false);
        }

        public async Task AddWeatherAsync(Weather weather, DateTime recievedTime)
        {
            if (_database is null)
            {
                throw new InvalidOperationException("Database is not ready");
            }

            await using var session = _database.AsyncSession();

            const string SearchQuery =
                   """
                    MATCH 
                        (a:Airport {icao: $icao}) 
                        -[u:USES WHERE 
                            u.weather < $weatherLevel
                            and u.dep <= $validTo
                            and $validFrom <= u.arr 
                        ]-> 
                        (f:Flight {recalculating: false}) 
                    return f.id;
                    """;

            var result = await session.RunAsync(SearchQuery, new
            {
                icao = weather.Airport,
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

                        await _flightRecalculation.PublishRecalculationAsync(fetched, weather.Id, DateTime.UtcNow - recievedTime).ConfigureAwait(false);
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