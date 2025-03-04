using DynamicFlightStorageSimulation;
using DynamicFlightStorageSimulation.DataCollection;
using DynamicFlightStorageSimulation.ExperimentOrchestrator.DataCollection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;

namespace ExperimentRunner
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            using ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));

            var logger = factory.CreateLogger<Program>();

            var builder = new ConfigurationBuilder()
               .SetBasePath(AppContext.BaseDirectory)
               .AddJsonFile("appsettings.json")
               .AddEnvironmentVariables()
               .AddUserSecrets<Program>();
            var configuration = builder.Build();

            var eventBusConfig = EventBusConfig.GetEmpty();
            configuration.GetSection("EventBusConfig").Bind(eventBusConfig);
            EnsureValid(eventBusConfig);

            using var simulationEventBus = new SimulationEventBus(eventBusConfig, factory.CreateLogger<SimulationEventBus>());
            await simulationEventBus.ConnectAsync();

            // DB Setup
            var dbContextOptions = new DbContextOptionsBuilder<DataCollectionContext>();
            var serverVersion = new MariaDbServerVersion(new Version(10, 5));
#if DEBUG
            dbContextOptions.EnableSensitiveDataLogging();
#endif
            dbContextOptions.UseMySql(configuration.GetConnectionString("ExperimentDataCollection"), serverVersion);
            var consumerDataLogger = new ConsumerDataLogger(dbContextOptions.Options);

            var weatherService = new WeatherService();

            // The event store to experiment with. Change me!
            var eventDataStore = new Neo4jDataStore.TimeBucketedNeo4jDataStore(weatherService, simulationEventBus);

            logger.LogInformation("Event data store of type {Type} ready", eventDataStore.GetType().FullName);
            using var consumer = new SimulationConsumer(simulationEventBus, weatherService, consumerDataLogger, eventDataStore, factory.CreateLogger<SimulationConsumer>());

            await consumer.StartAsync();
            logger.LogInformation("Simulation consumer started");

            Console.CancelKeyPress += async delegate
            {
                logger.LogInformation("Shutting down...");
                await simulationEventBus.DisconnectAsync();
                consumer.Dispose();
                logger.LogInformation("Good bye :(");
                await Task.Delay(100);
                Environment.Exit(0);
            };

            logger.LogInformation("Press CTRL+C to exit");
            await Task.Delay(-1); // Wait forever
        }

        private static void EnsureValid(object o)
        {
            var validationErrors = new List<ValidationResult>();
            Validator.TryValidateObject(o, new ValidationContext(o), validationErrors, true);

            if (validationErrors.Any())
            {
                var message = $"Configuration invalid:\n" + string.Join("\n", validationErrors.Select(e => e.ErrorMessage));
                throw new InvalidOperationException(message);
            }
        }
    }
}
