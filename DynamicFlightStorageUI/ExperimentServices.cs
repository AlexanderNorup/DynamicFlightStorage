﻿using DynamicFlightStorageSimulation.ExperimentOrchestrator;
using DynamicFlightStorageSimulation;
using Microsoft.Extensions.Options;
using DynamicFlightStorageSimulation.ExperimentOrchestrator.DataCollection;
using Microsoft.EntityFrameworkCore;
using DynamicFlightStorageSimulation.DataCollection;
using DynamicFlightStorageDTOs;

namespace DynamicFlightStorageUI
{
    public static class ExperimentServices
    {
        internal static void AddExperimentServices(this WebApplicationBuilder builder)
        {
            builder.Services.AddOptions<EventBusConfig>()
                .Bind(builder.Configuration.GetSection("EventBusConfig"))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            builder.Services.AddSingleton((s) =>
            {
                var config = s.GetService<IOptions<EventBusConfig>>()!.Value;
                var bus = new SimulationEventBus(config, s.GetRequiredService<ILogger<SimulationEventBus>>());
                return bus;
            });

            builder.Services.AddHostedService<EventBusConnector>();

            builder.Services.AddSingleton<LatencyTester>();

            builder.Services.AddSingleton((s) =>
            {
                var config = s.GetService<IOptions<EventBusConfig>>()!.Value;
                return new ConsumingMonitor(config);
            });

            builder.Services.AddDbContext<DataCollectionContext>((s, options) =>
            {
                var serverVersion = new MariaDbServerVersion(new Version(10, 5));
#if DEBUG
                options.EnableSensitiveDataLogging();
#endif
                options.UseMySql(builder.Configuration.GetConnectionString("ExperimentDataCollection"), serverVersion);
            }, ServiceLifetime.Transient, ServiceLifetime.Singleton);


            builder.Services.AddSingleton<ConsumerDataLogger>();
            builder.Services.AddSingleton<ExperimentDataCollector>();

            builder.Services.AddSingleton((s) =>
            {
                var eventBus = s.GetRequiredService<SimulationEventBus>();
                var dataPath = builder.Configuration["DataSetPath"];
                if (dataPath is null)
                {
                    throw new InvalidOperationException("DataSetPath must be set in the configuration.");
                }
                return new DataSetManager(dataPath, eventBus);
            });

            builder.Services.AddOptions<PushoverOptions>()
                .Bind(builder.Configuration.GetSection("PushoverOptions"))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            builder.Services.AddHttpClient("pushover", (s, client) =>
            {
                var options = s.GetRequiredService<IOptions<PushoverOptions>>().Value;
                client.BaseAddress = new Uri(options.Endpoint);
                client.Timeout = TimeSpan.FromSeconds(5);
            });

            builder.Services.AddSingleton<IExperimentNotifier, PushoverExperimentNotifier>();

            builder.Services.AddSingleton<Orchestrator>();
        }
    }
}
