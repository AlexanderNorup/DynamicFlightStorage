using DynamicFlightStorageDTOs;
using DynamicFlightStorageSimulation.Events;
using Microsoft.Extensions.Logging;

namespace DynamicFlightStorageSimulation
{
    public class SimulationConsumer : IDisposable
    {
        private ILogger<SimulationConsumer>? _logger;
        private SimulationEventBus _simulationEventBus;
        private WeatherService _weatherService;
        private IEventDataStore _eventDataStore;
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
        }

        public async Task StartAsync()
        {
            await _simulationEventBus.ConnectAsync(withFlightTopic: true,
                withWeatherTopic: true,
                withRecalculationTopic: false).ConfigureAwait(false);
        }

        private async Task OnWeatherRecieved(WeatherEvent e)
        {
            _weatherService.AddWeather(e.Weather);
            await _eventDataStore.AddWeatherAsync(e.Weather).ConfigureAwait(false);
            _logger?.LogDebug("Processed weather event: {Weather}", e.Weather);
        }

        private async Task OnFlightRecieved(FlightStorageEvent e)
        {
            await _eventDataStore.AddOrUpdateFlightAsync(e.Flight).ConfigureAwait(false);
            _logger?.LogDebug("Processed flight event: {Flight}", e.Flight);
        }

        private async Task OnSystemMessage(SystemMessageEvent e)
        {
            var message = e.SystemMessage;
            switch (message.MessageType)
            {
                case SystemMessage.SystemMessageType.LatencyRequest:
                    await _simulationEventBus.PublishSystemMessage(new()
                    {
                        Message = message.Message,
                        TimeStamp = message.TimeStamp,
                        MessageType = SystemMessage.SystemMessageType.LatencyResponse,
                        Source = _simulationEventBus.ClientId
                    }).ConfigureAwait(false);
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
