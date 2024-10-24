using DynamicFlightStorageDTOs;
using DynamicFlightStorageSimulation.Events;
using Microsoft.Extensions.Logging;

namespace DynamicFlightStorageSimulation
{
    public class SimulationConsumer : IDisposable
    {
        private ILogger<SimulationConsumer>? _logger;
        private SimulationEventBus _simulationEventBus;
        private IEventDataStore _eventDataStore;
        private bool disposedValue;

        public SimulationConsumer(SimulationEventBus simulationEventBus, IEventDataStore eventDataStore, ILogger<SimulationConsumer> logger)
        {
            _logger = logger;
            _simulationEventBus = simulationEventBus ?? throw new ArgumentNullException(nameof(simulationEventBus));
            _eventDataStore = eventDataStore ?? throw new ArgumentNullException(nameof(eventDataStore));
            _simulationEventBus.SubscribeToFlightStorageEvent(OnFlightRecieved);
            _simulationEventBus.SubscribeToWeatherEvent(OnWeatherRecieved);
        }

        public async Task StartAsync()
        {
            await _simulationEventBus.ConnectAsync(withFlightTopic: true,
                withWeatherTopic: true,
                withRecalculationTopic: false).ConfigureAwait(false);
        }

        private async Task OnWeatherRecieved(WeatherEvent e)
        {
            await _eventDataStore.AddWeatherAsync(e.Weather).ConfigureAwait(false);
            _logger?.LogDebug("Processed weather event: {Weather}", e.Weather);
        }

        private async Task OnFlightRecieved(FlightStorageEvent e)
        {
            await _eventDataStore.AddOrUpdateFlightAsync(e.Flight).ConfigureAwait(false);
            _logger?.LogDebug("Processed flight event: {Flight}", e.Flight);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _simulationEventBus.UnSubscribeToFlightStorageEvent(OnFlightRecieved);
                    _simulationEventBus.UnSubscribeToWeatherEvent(OnWeatherRecieved);
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
