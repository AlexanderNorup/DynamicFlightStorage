using DynamicFlightStorageSimulation;
using DynamicFlightStorageSimulation.DataCollection;
using DynamicFlightStorageSimulation.ExperimentOrchestrator.DataCollection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.CommandLine;
using System.ComponentModel.DataAnnotations;

namespace ExperimentRunner
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var rootCommand = new RootCommand();

            var dataArgument = new Argument<DataStoreType>("dataStoreType", "The type of the event data store to use");
            rootCommand.AddArgument(dataArgument);
            rootCommand.SetHandler(RunDataStore, dataArgument);
            await rootCommand.InvokeAsync(args).ConfigureAwait(false);
        }

        static async Task RunDataStore(DataStoreType dataStoreType)
        {
            using ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));

            var logger = factory.CreateLogger<Program>();

            logger.LogInformation("Starting with data store type parsed as {Type}", dataStoreType);

            var builder = new ConfigurationBuilder()
               .SetBasePath(AppContext.BaseDirectory)
               .AddJsonFile("appsettings.json")
               .AddEnvironmentVariables()
               .AddUserSecrets<Program>();
            var configuration = builder.Build();

            var eventBusConfig = EventBusConfig.GetEmpty();
            eventBusConfig.FriendlyClientName = $"ExperimentRunner_{dataStoreType}";
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
            var consumerDataLogger = new ConsumerDataLogger(dbContextOptions.Options, factory.CreateLogger<ConsumerDataLogger>());

            var weatherService = new WeatherService();

            // The event store to experiment with. Change me!
            //var eventDataStore = new GPUAcceleratedEventDataStore.CUDAEventDataStore(weatherService, simulationEventBus);
            var eventDataStore = dataStoreType.CreateDataStore(weatherService, simulationEventBus);

            logger.LogInformation("Event data store of type {Type} ready", eventDataStore.GetType().FullName);
            using var consumer = new SimulationConsumer(simulationEventBus, weatherService, consumerDataLogger, eventDataStore, factory.CreateLogger<SimulationConsumer>());

            await consumer.StartAsync().ConfigureAwait(false);
            logger.LogInformation("Simulation consumer started");
            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += async delegate
            {
                cts.Cancel();
                logger.LogInformation("Shutting down...");
                await simulationEventBus.DisconnectAsync().ConfigureAwait(false);
                consumer.Dispose();
                logger.LogInformation("Good bye :(");
                await Task.Delay(100).ConfigureAwait(false);
                Environment.Exit(0);
            };

            logger.LogInformation("Press CTRL+C to exit");
            await Task.Delay(-1, cts.Token); // Wait untill cancelled
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
