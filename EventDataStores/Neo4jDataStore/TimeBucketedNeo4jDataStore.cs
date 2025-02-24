using DynamicFlightStorageDTOs;
using Neo4j.Driver;
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
            const string TimeBucketCreationQuery =
                """
                MERGE (a:Airport {icao: $icao})
                    WITH a
                MERGE (a) -[h:HasFlightIn]-> (t:TimeBucket {icao: $icao, timeSliceStart: $timeSliceStart})
                    WITH t
                MATCH (f:Flight {id: $flightId})
                MERGE (t) -[e:USES]-> (f)
                SET e.weather = $weatherLevel, e.dep = $departure, e.arr = $arrival;
                """;

            await tx.RunAsync(TimeBucketCreationQuery, new
            {
                icao,
                timeSliceStart = ((DateTimeOffset)GetTimeSlot(departure)).ToUnixTimeSeconds(),
                flightId,
                weatherLevel = (int)weatherCategory,
                departure = ((DateTimeOffset)departure).ToUnixTimeSeconds(),
                arrival = ((DateTimeOffset)arrival).ToUnixTimeSeconds()
            }).ConfigureAwait(false);
        }

        private static DateTime GetTimeSlot(DateTime dateTime)
        {
            var ticksLeftOver = dateTime.Ticks % TimeBucketSize.Ticks;
            var roundedDown = dateTime.Subtract(TimeSpan.FromTicks(ticksLeftOver));
            return roundedDown;
        }


        public async Task AddWeatherAsync(Weather weather)
        {

            throw new NotImplementedException();
        }

        public async Task DeleteFlightAsync(string id)
        {
            throw new NotImplementedException();
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
