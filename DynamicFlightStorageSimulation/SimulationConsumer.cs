using DynamicFlightStorageDTOs;
using Microsoft.Extensions.Logging;

namespace DynamicFlightStorageSimulation
{
    public class SimulationConsumer : IDisposable
    {
        private ILogger<SimulationConsumer>? _logger;
        private SimulationEventBus _simulationEventBus;
        private WeatherService _weatherService;
        private IEventDataStore _eventDataStore;
        private bool cleanState;
        private bool disposedValue;

        public SimulationConsumer(SimulationEventBus simulationEventBus, WeatherService weatherService, IEventDataStore eventDataStore, ILogger<SimulationConsumer> logger)
        {
            _logger = logger;
            _simulationEventBus = simulationEventBus ?? throw new ArgumentNullException(nameof(simulationEventBus));
            _weatherService = weatherService ?? throw new ArgumentNullException(nameof(weatherService));
            _eventDataStore = eventDataStore ?? throw new ArgumentNullException(nameof(eventDataStore));
            _simulationEventBus.SubscribeToFlightStorageEvent(OnFlightRecieved);
            _simulationEventBus.SubscribeToWeatherEvent(OnWeatherRecieved);
            _simulationEventBus.SubscribeToSystemEvent(OnSystemMessage);
            cleanState = true;
        }

        public string ClientId => _simulationEventBus.ClientId;

        public async Task StartAsync()
        {
            if (!_simulationEventBus.IsConnected())
            {
                await _simulationEventBus.ConnectAsync().ConfigureAwait(false);
            }
        }

        private async Task OnWeatherRecieved(Weather weather)
        {
            cleanState = false;
            _weatherService.AddWeather(weather);
            await _eventDataStore.AddWeatherAsync(weather).ConfigureAwait(false);
            //_logger?.LogDebug("Processed weather event: {Weather}", e.Weather);
        }

        private async Task OnFlightRecieved(Flight flight)
        {
            cleanState = false;
            await _eventDataStore.AddOrUpdateFlightAsync(flight).ConfigureAwait(false);
            //_logger?.LogDebug("Processed flight event: {Flight}", e.Flight);
        }

        private async Task ResetStateAsync(string experimentId)
        {
            _weatherService.Clear();
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

            await _simulationEventBus.SubscribeToExperiment(experimentId).ConfigureAwait(false);

            // Signal ready
            await _simulationEventBus.PublishSystemMessage(new SystemMessage()
            {
                Message = experimentId,
                MessageType = SystemMessage.SystemMessageType.NewExperimentReady,
                Source = _simulationEventBus.ClientId
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
                        Source = _simulationEventBus.ClientId
                    }).ConfigureAwait(false);
                    break;
                case SystemMessage.SystemMessageType.NewExperiment:
                    await ResetStateAsync(message.Message).ConfigureAwait(false);
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
