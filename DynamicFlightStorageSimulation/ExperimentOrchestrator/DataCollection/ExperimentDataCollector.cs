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
            await _eventBus.SubscribeToRecalculationEventAsync(OnRecalculationRecieved);
        }

        public void StopMonitoringExperiment()
        {
            _eventBus.UnSubscribeToRecalculationEvent(OnRecalculationRecieved);
        }

        public async Task FinishDataCollectionAsync()
        {
            _context.RecalculationEventLogs.AddRange(_recalculationLogs);
            _recalculationLogs = new();
            await SaveChangesAsyncSafely().ConfigureAwait(false);
        }

        private ConcurrentBag<RecalculationEventLog> _recalculationLogs = new();

        private DateTime _lastUpdated = DateTime.UtcNow;
        private const int _updateIntervalMs = 10_000;
        private SemaphoreSlim _updateSemaphore = new(1, 1);
        private bool _updateIsPending = false;

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
            if (_updateIsPending)
            {
                // We're already updating and there is already a pending update
                return;
            }
            _updateIsPending = true;
            await _updateSemaphore.WaitAsync().ConfigureAwait(false);

            try
            {
                if (!_updateIsPending)
                {
                    return;
                }
                await _context.SaveChangesAsync().ConfigureAwait(false);
            }
            finally
            {
                _updateIsPending = false;
                _updateSemaphore.Release();
            }
        }

        public void Dispose()
        {
            StopMonitoringExperiment();
        }
    }
}
