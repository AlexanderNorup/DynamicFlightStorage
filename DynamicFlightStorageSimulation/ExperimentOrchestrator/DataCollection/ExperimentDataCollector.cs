using DynamicFlightStorageDTOs;
using DynamicFlightStorageSimulation.ExperimentOrchestrator.DataCollection.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace DynamicFlightStorageSimulation.ExperimentOrchestrator.DataCollection
{
    public class ExperimentDataCollector : IDisposable
    {
        private SimulationEventBus _eventBus;
        private DataCollectionContext _context;
        public ExperimentDataCollector(SimulationEventBus eventBus, DataCollectionContext context)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task MonitorExperimentAsync(string experimentId)
        {
            // Capturing all events is a bit obsessive However if we wanted to do that, the code below can be enabled.
            //await _eventBus.SubscribeToExperiment(experimentId);
            //_eventBus.SubscribeToFlightStorageEvent(OnFlightRecieved);
            //_eventBus.SubscribeToWeatherEvent(OnWeatherRecieved);
            await _eventBus.SubscribeToRecalculationEventAsync(OnRecalculationRecieved);
        }

        public void StopMonitoringExperiment()
        {
            //_eventBus.UnSubscribeToFlightStorageEvent(OnFlightRecieved);
            //_eventBus.UnSubscribeToWeatherEvent(OnWeatherRecieved);
            _eventBus.UnSubscribeToRecalculationEvent(OnRecalculationRecieved);
        }

        public async Task FinishDataCollectionAsync()
        {
            int totalCounnt = _flightLogs.Count + _weatherLogs.Count + _recalculationLogs.Count;
            _context.FlightEventLogs.AddRange(_flightLogs);
            _context.WeatherEventLogs.AddRange(_weatherLogs);
            _context.RecalculationEventLogs.AddRange(_recalculationLogs);
            _recalculationLogs = new();
            _flightLogs = new();
            _weatherLogs = new();
            await SaveChangesAsyncSafely().ConfigureAwait(false);
        }

        private ConcurrentBag<RecalculationEventLog> _recalculationLogs = new();
        private ConcurrentBag<FlightEventLog> _flightLogs = new();
        private ConcurrentBag<WeatherEventLog> _weatherLogs = new();

        private DateTime _lastUpdated = DateTime.UtcNow;
        private const int _updateIntervalMs = 10_000;
        private SemaphoreSlim _updateSemaphore = new(1, 1);

        private Task OnWeatherRecieved(WeatherEvent weatherEvent)
        {
            _weatherLogs.Add(new WeatherEventLog()
            {
                ExperimentId = _eventBus.CurrentExperimentId,
                UtcTimeStamp = weatherEvent.TimeStamp,
                WeatherId = weatherEvent.Weather.Id
            });

            return Task.CompletedTask;
        }

        private Task OnFlightRecieved(FlightEvent flight)
        {
            _flightLogs.Add(new FlightEventLog()
            {
                ExperimentId = _eventBus.CurrentExperimentId,
                UtcTimeStamp = flight.TimeStamp,
                FlightId = flight.Flight.FlightIdentification
            });
            return Task.CompletedTask;
        }

        private Task OnRecalculationRecieved(FlightRecalculation flight)
        {
            _recalculationLogs.Add(new RecalculationEventLog()
            {
                ExperimentId = _eventBus.CurrentExperimentId,
                UtcTimeStamp = flight.RecalculatedTime,
                FlightId = flight.FlightIdentification
            });

            return Task.CompletedTask;
        }

        public async Task AddOrUpdateExperimentAsync(Experiment experiment)
        {
            ArgumentNullException.ThrowIfNull(experiment);
            if (await _context.Experiments.ContainsAsync(experiment))
            {
                _context.Experiments.Update(experiment);
            }
            else
            {
                _context.Experiments.Add(experiment);
            }

            await SaveChangesAsyncSafely().ConfigureAwait(false);
        }

        public async Task AddOrUpdateExperimentResultAsync(ExperimentResult result)
        {
            ArgumentNullException.ThrowIfNull(result);

            _context.ExperimentResults.Update(result);

            await SaveChangesAsyncSafely().ConfigureAwait(false);
        }

        public async Task SaveChangesAsyncSafely()
        {
            await _updateSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                await _context.SaveChangesAsync().ConfigureAwait(false);
            }
            finally
            {
                _updateSemaphore.Release();
            }
        }

        public void Dispose()
        {
            StopMonitoringExperiment();
        }
    }
}
