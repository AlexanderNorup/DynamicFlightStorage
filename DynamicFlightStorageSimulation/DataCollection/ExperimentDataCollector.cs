using DynamicFlightStorageDTOs;
using DynamicFlightStorageSimulation.ExperimentOrchestrator.DataCollection.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using Timer = System.Timers.Timer;

namespace DynamicFlightStorageSimulation.ExperimentOrchestrator.DataCollection
{
    public class ExperimentDataCollector : IDisposable
    {
        private SimulationEventBus _eventBus;
        private DataCollectionContext _context;

        private ConcurrentBag<RecalculationEventLog> _recalculationLogs = new();

        private Timer _updateTimer;
        private DateTime _lastUpdated = DateTime.UtcNow;
        private const int _updateIntervalMs = 30_000;
        private SemaphoreSlim _updateSemaphore = new(1, 1);
        private bool _updateIsPending = false;

        public ExperimentDataCollector(SimulationEventBus eventBus, DbContextOptions<DataCollectionContext> dbContextOptions)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _context = new DataCollectionContext(dbContextOptions ?? throw new ArgumentNullException(nameof(dbContextOptions)));
            _updateTimer = new Timer();
            _updateTimer.Interval = _updateIntervalMs;
            _updateTimer.Elapsed += async (sender, e) =>
            {
                if (!_updateIsPending
                    && _lastUpdated.AddMilliseconds(_updateIntervalMs) < DateTime.UtcNow)
                {
                    if (_recalculationLogs.Count > 0)
                    {
                        await SaveChangesAsyncSafely().ConfigureAwait(false);
                    }
                    else
                    {
                        _lastUpdated = DateTime.UtcNow;
                    }
                }
            };
        }

        public async Task MonitorExperimentAsync(string experimentId)
        {
            await _eventBus.SubscribeToRecalculationEventAsync(OnRecalculationRecieved);
            _updateTimer.Start();
        }

        public void StopMonitoringExperiment()
        {
            _updateTimer.Stop();
            _eventBus.UnSubscribeToRecalculationEvent(OnRecalculationRecieved);
            _context.ChangeTracker.Clear();
        }

        public async Task FinishDataCollectionAsync(HashSet<string> experimentClientIds)
        {
            await _eventBus.PublishSystemMessage(new SystemMessage()
            {
                Message = _eventBus.CurrentExperimentId,
                MessageType = SystemMessage.SystemMessageType.ExperimentComplete,
                Source = _eventBus.ClientId,
                Targets = experimentClientIds,
                TimeStamp = DateTime.UtcNow
            });
            await SaveChangesAsyncSafely().ConfigureAwait(false);
        }

        private Task OnRecalculationRecieved(FlightRecalculation flight)
        {
            _recalculationLogs.Add(new RecalculationEventLog()
            {
                ExperimentId = _eventBus.CurrentExperimentId,
                ClientId = _eventBus.ClientId,
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

                if (_recalculationLogs.Count > 0)
                {
                    var recalculationLogs = _recalculationLogs;
                    _recalculationLogs = new();
                    _context.RecalculationEventLogs.AddRange(recalculationLogs);
                }

                await _context.SaveChangesAsync().ConfigureAwait(false);
            }
            finally
            {
                _updateIsPending = false;
                _updateSemaphore.Release();
                _lastUpdated = DateTime.UtcNow;
            }
        }

        public void Dispose()
        {
            StopMonitoringExperiment();
        }
    }
}
