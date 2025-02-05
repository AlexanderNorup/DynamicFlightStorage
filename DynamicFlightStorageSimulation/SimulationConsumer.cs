using DynamicFlightStorageDTOs;
using DynamicFlightStorageSimulation.DataCollection;
using DynamicFlightStorageSimulation.ExperimentOrchestrator.DataCollection.Entities;
using Microsoft.Extensions.Logging;

namespace DynamicFlightStorageSimulation
{
    public class SimulationConsumer : IDisposable
    {
        private ILogger<SimulationConsumer>? _logger;
        private SimulationEventBus _simulationEventBus;
        private WeatherService _weatherService;
        private IEventDataStore _eventDataStore;
        private ConsumerDataLogger _consumerLogger;
        private bool cleanState;
        private bool disposedValue;

        public SimulationConsumer(SimulationEventBus simulationEventBus, WeatherService weatherService, ConsumerDataLogger consumerDataLogger, IEventDataStore eventDataStore, ILogger<SimulationConsumer> logger)
        {
            _logger = logger;
            _simulationEventBus = simulationEventBus ?? throw new ArgumentNullException(nameof(simulationEventBus));
            _weatherService = weatherService ?? throw new ArgumentNullException(nameof(weatherService));
            _consumerLogger = consumerDataLogger ?? throw new ArgumentNullException(nameof(consumerDataLogger));
            _eventDataStore = eventDataStore ?? throw new ArgumentNullException(nameof(eventDataStore));
            _simulationEventBus.SubscribeToFlightStorageEvent(OnFlightRecieved);
            _simulationEventBus.SubscribeToWeatherEvent(OnWeatherRecieved);
            _simulationEventBus.SubscribeToSystemEvent(OnSystemMessage);
            cleanState = true;
        }

        public string ClientId => _simulationEventBus.ClientId;
        public string EventDataStoreName => _eventDataStore.GetType()?.FullName ?? "Unknown event-datastore";

        public async Task StartAsync()
        {
            if (!_simulationEventBus.IsConnected())
            {
                await _simulationEventBus.ConnectAsync().ConfigureAwait(false);
            }
            await _eventDataStore.StartAsync().ConfigureAwait(false);
        }

        private async Task OnWeatherRecieved(WeatherEvent weatherEvent)
        {
            _consumerLogger.LogWeatherData(weatherEvent);
            cleanState = false;
            var weather = weatherEvent.Weather;
            _weatherService.AddWeather(weather);
            await _eventDataStore.AddWeatherAsync(weather).ConfigureAwait(false);
        }

        private async Task OnFlightRecieved(FlightEvent flight)
        {
            _consumerLogger.LogFlightData(flight);
            cleanState = false;
            await _eventDataStore.AddOrUpdateFlightAsync(flight.Flight).ConfigureAwait(false);
        }

        private async Task ResetStateAsync(SystemMessage message)
        {
            var experimentId = message.Message;
            _weatherService.Clear();
            _consumerLogger.ResetLogger();
            if (!cleanState)
            {
                // Try to reset the EventDataStore
                try
                {
                    await _eventDataStore.ResetAsync().ConfigureAwait(false);
                    cleanState = true;
                }
                catch (Exception e)
                {
                    _logger?.LogError(e, "Error resetting state");
                    return;
                }
            }

            // Check for logging
            bool shouldLog = false; // Default to false if we don't get any info
            if (message.Data is { } data)
            {
                if (data.TryGetValue(nameof(Experiment.LoggingEnabled), out var loggingEnabled)
                    && loggingEnabled is bool loggingEnabledBool)
                {
                    shouldLog = loggingEnabledBool;
                }
            }

            _logger?.LogInformation("Logging enabled: {LoggingEnabled}", shouldLog);
            _consumerLogger.IsLoggingEnabled = shouldLog;

            await _simulationEventBus.SubscribeToExperiment(experimentId).ConfigureAwait(false);

            // Signal ready
            await _simulationEventBus.PublishSystemMessage(new SystemMessage()
            {
                Message = experimentId,
                MessageType = SystemMessage.SystemMessageType.NewExperimentReady,
                Source = _simulationEventBus.ClientId,
            });
        }

        private async Task OnSystemMessage(SystemMessage message)
        {
            if (message.Targets.Count > 0 && !message.Targets.Contains(_simulationEventBus.ClientId))
            {
                // Message not meant for this consumer.
                return;
            }
            switch (message.MessageType)
            {
                case SystemMessage.SystemMessageType.LatencyRequest:
                    await _simulationEventBus.PublishSystemMessage(new SystemMessage()
                    {
                        Message = message.Message,
                        TimeStamp = message.TimeStamp,
                        MessageType = SystemMessage.SystemMessageType.LatencyResponse,
                        Source = _simulationEventBus.ClientId,
                        Data = new Dictionary<string, object>()
                        {
                            { "DataStoreName", EventDataStoreName }
                        },
                    }).ConfigureAwait(false);
                    break;
                case SystemMessage.SystemMessageType.NewExperiment:
                    await ResetStateAsync(message).ConfigureAwait(false);
                    break;
                case SystemMessage.SystemMessageType.ExperimentComplete:
                    await _consumerLogger.PersistDataAsync(_simulationEventBus.CurrentExperimentId).ConfigureAwait(false);
                    _consumerLogger.ResetLogger();
                    break;
                default:
                    break;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _simulationEventBus.UnSubscribeToFlightStorageEvent(OnFlightRecieved);
                    _simulationEventBus.UnSubscribeToWeatherEvent(OnWeatherRecieved);
                    _simulationEventBus.UnSubscribeToSystemEvent(OnSystemMessage);
                }
                if (_eventDataStore is IDisposable disposable)
                {
                    disposable.Dispose();
                }
                _eventDataStore = null!;
                _simulationEventBus = null!;
                disposedValue = true;
            }
        }
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
