﻿using DynamicFlightStorageSimulation;
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

            var weatherService = new WeatherService();

            // The event store to experiment with. Change me!
            var eventDataStore = new BasicEventDataStore.BasicEventDataStore(weatherService, simulationEventBus);

            logger.LogInformation("Event data store of type {Type} ready", eventDataStore.GetType().FullName);
            using var consumer = new SimulationConsumer(simulationEventBus, weatherService, eventDataStore, factory.CreateLogger<SimulationConsumer>());

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
