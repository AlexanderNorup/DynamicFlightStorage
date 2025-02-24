using DynamicFlightStorageDTOs;
using Neo4j.Driver;
using Testcontainers.Neo4j;

namespace Neo4jDataStore
{
    public class TimeBucketedNeo4jDataStore : IEventDataStore, IDisposable
    {
        private Neo4jContainer? _container;
        private IDriver? _database;
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

            throw new NotImplementedException();
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
