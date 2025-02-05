using DynamicFlightStorageSimulation.ExperimentOrchestrator;
using DynamicFlightStorageSimulation;
using Microsoft.Extensions.Options;
using DynamicFlightStorageSimulation.ExperimentOrchestrator.DataCollection;
using Microsoft.EntityFrameworkCore;
using DynamicFlightStorageSimulation.DataCollection;

namespace DynamicFlightStorageUI
{
    public static class ExperimentServices
    {
        internal static void AddExperimentServices(this WebApplicationBuilder builder)
        {
            builder.Services.AddOptions<EventBusConfig>()
                .Bind(builder.Configuration.GetSection("EventBusConfig"))
                .ValidateDataAnnotations();

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
                var metar = builder.Configuration["WeatherMetarFiles"];
                var taf = builder.Configuration["WeatherTafFiles"];
                if (metar is null || taf is null)
                {
                    throw new InvalidOperationException("WeatherMetarFiles and WeatherTafFiles must be set in the configuration.");
                }
                return new WeatherInjector(eventBus, metar, taf);
            });

            builder.Services.AddSingleton((s) =>
            {
                var eventBus = s.GetRequiredService<SimulationEventBus>();
                var flights = builder.Configuration["FlightFiles"];
                if (flights is null)
                {
                    throw new InvalidOperationException("FlightFiles must be set in the configuration.");
                }
                return new FlightInjector(eventBus, flights);
            });

            builder.Services.AddSingleton<Orchestrator>();
        }
    }
}
