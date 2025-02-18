using DynamicFlightStorageDTOs;
using DynamicFlightStorageSimulation.ExperimentOrchestrator.DataCollection;
using DynamicFlightStorageSimulation.ExperimentOrchestrator.DataCollection.Entities;
using DynamicFlightStorageSimulation.Utilities;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Diagnostics;
using static DynamicFlightStorageDTOs.SystemMessage;

namespace DynamicFlightStorageSimulation.ExperimentOrchestrator
{
    public class Orchestrator : IDisposable
    {
        public const int LatencyTestSamples = 20;
        public const int LatencyTestFrequencyMs = 100;
        public const int ExperimentLoopIntervalMs = 900;

        private SimulationEventBus _eventBus;
        private LatencyTester _latencyTester;
        private ConsumingMonitor _consumingMonitor;
        private DataSetManager _dataSetManager;
        private ExperimentDataCollector _experimentDataCollector;
        private EventLogger<Orchestrator> _logger;
        private System.Timers.Timer _experimentChecker;
        private SemaphoreSlim _experimentControllerSemaphore = new SemaphoreSlim(1, 1);

        private WeatherInjector? _weatherInjector;
        private FlightInjector? _flightInjector;

        public Orchestrator(SimulationEventBus eventBus, LatencyTester latencyTester, ConsumingMonitor consumingMonitor, DataSetManager dataSetManager, ExperimentDataCollector experimentDataCollector, ILogger<Orchestrator> logger)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _latencyTester = latencyTester ?? throw new ArgumentNullException(nameof(latencyTester));
            _consumingMonitor = consumingMonitor ?? throw new ArgumentNullException(nameof(consumingMonitor));
            _dataSetManager = dataSetManager ?? throw new ArgumentNullException(nameof(dataSetManager));
            _experimentDataCollector = experimentDataCollector ?? throw new ArgumentNullException(nameof(experimentDataCollector));
            _experimentDataCollector.OnRecalculation += RecalculationMessageRecieved;
            _logger = new EventLogger<Orchestrator>(logger);
            _experimentChecker = new System.Timers.Timer(1000);
            _experimentChecker.Elapsed += (s, e) => CheckExperimentRunning();
            _experimentChecker.AutoReset = true;

            _logger.OnLog += OnLog;
        }

        private void RecalculationMessageRecieved(string obj)
        {
            throw new NotImplementedException();
        }

        private const int LogsToKeep = 30;
        private object _logLock = new object();
        private LinkedList<LogEntry> _logCache = new LinkedList<LogEntry>();

        // You might ask yourself why I don't just call "OnLog" instead of "_logger.____".
        // The reason being that I might want to give my ILogger to for example the FlightInjector. In this case,
        // I also want the events from inside FlightInjector to be sent to anyone potentially listening. 
        // Therefore I need to wrap the event and re-cast it to anyone listening on the Orchestrator.
        public event OnLogEvent? OnLogEvent;
        private void OnLog(LogEntry e)
        {
            OnLogEvent?.Invoke(e);
            lock (_logLock)
            {
                _logCache.AddLast(e);
                if (_logCache.Count > LogsToKeep)
                {
                    _logCache.RemoveFirst();
                }
            }
        }

        // This is used to pre-populate the logs when you're just subscribing to the event.
        public List<LogEntry> GetLogCache()
        {
            lock (_logLock)
            {
                return new List<LogEntry>(_logCache);
            }
        }

        private HashSet<string> _experimentRunnerClientIds = new HashSet<string>();
        public HashSet<string> ExperimentRunnerClientIds
        {
            get => new HashSet<string>(_experimentRunnerClientIds); // Copying to new HashSet to ensure immutability
            set
            {
                if (value is not null)
                {
                    if (OrchestratorState != OrchestratorState.Idle || CheckExperimentRunning())
                    {
                        _logger.LogWarning("The ExperimentRunnerIds cannot be changed while an experiment is running.");
                        return;
                    }
                    _experimentRunnerClientIds = value;
                    OnExperimentStateChanged?.Invoke();
                }
            }
        }

        public Experiment? CurrentExperiment { get; private set; }
        public Task? ExperimentTask { get; private set; }
        public Task? ExperimentConsumerLagTask { get; private set; }
        public ExperimentResult? CurrentExperimentResult { get; private set; }
        public Dictionary<string, (int flightLag, int weatherLag)> CurrentLag { get; private set; } = new();

        public OrchestratorState OrchestratorState { get; private set; } = OrchestratorState.Idle;

        public DateTime? CurrentSimulationTime { get; private set; }
        public DateTime? LastUpdateTime { get; private set; }
        public CancellationTokenSource ExperimentCancellationToken { get; private set; } = new();

        public event Action? OnExperimentStateChanged;

        public void SetExperiment(Experiment experiment)
        {
            _ = experiment ?? throw new ArgumentNullException(nameof(experiment));
            if (OrchestratorState != OrchestratorState.Idle
                && !CheckExperimentRunning())
            {
                throw new InvalidOperationException($"An experiment can only be set when the state is {OrchestratorState.Idle}.");
            }

            var injectors = _dataSetManager.GetInjectorsForDataSet(experiment.DataSetName);
            _flightInjector = injectors.FlightInjector;
            _weatherInjector = injectors.WeatherInjector;

            CurrentExperiment = experiment;
            CurrentExperimentResult = null;

            ResetExperimentState();
        }

        public async Task RunExperimentPreloadAsync()
        {
            if (ExperimentRunnerClientIds.Count == 0)
            {
                _logger.LogError("Cannot start preload because there is no ExperimentRunnerClientIds set");
                return;
            }
            if (CurrentExperiment is null)
            {
                throw new InvalidOperationException("No experiment is currently set.");
            }
            if (_flightInjector is null || _weatherInjector is null)
            {
                throw new InvalidOperationException("FlightInjector and WeatherInjector are not set.");
            }
            if (CheckExperimentRunning())
            {
                throw new InvalidOperationException("An experiment is detected to be running");
            }
            if (OrchestratorState != OrchestratorState.Idle)
            {
                throw new InvalidOperationException($"Cannot start preload because the orchestrator is not in {OrchestratorState.Idle} state.");
            }
            await _experimentControllerSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                if (OrchestratorState != OrchestratorState.Idle)
                {
                    return;
                }

                _logger.LogInformation("Doing Experiment Preload");
                OrchestratorState = OrchestratorState.Preloading;
                OnExperimentStateChanged?.Invoke();

                await _eventBus.CreateNewExperiment(CurrentExperiment.Id).ConfigureAwait(false);
                await _experimentDataCollector.AddOrUpdateExperimentAsync(CurrentExperiment);

                ExperimentCancellationToken = new CancellationTokenSource();
                var minimumWaitPreloadWaitTime = Task.Delay(TimeSpan.FromSeconds(10));

                var result = await SendSystemMessageAndWaitForResponseAsync(new SystemMessage()
                {
                    Message = CurrentExperiment.Id,
                    MessageType = SystemMessageType.NewExperiment,
                    Source = _eventBus.ClientId,
                    Targets = ExperimentRunnerClientIds,
                    Data = new()
                    {
                        { nameof(Experiment.LoggingEnabled), CurrentExperiment.LoggingEnabled },
                    }
                }, SystemMessageType.NewExperimentReady, TimeSpan.FromSeconds(10))
                    .ConfigureAwait(false);

                if (!result.success)
                {
                    throw new InvalidOperationException("One or more experiment runners did not respond in time.\n" +
                        $"Missing response(s) from {string.Join(",", ExperimentRunnerClientIds.Except(result.responses.Select(x => x.Source)))}");
                }


                // Do preload here.
                var st = Stopwatch.StartNew();
                _weatherInjector.SkipWeatherUntil(CurrentExperiment.SimulatedPreloadStartTime, ExperimentCancellationToken.Token);
                await _weatherInjector.PublishWeatherUntil(CurrentExperiment.SimulatedPreloadEndTime, CurrentExperiment.Id, _logger, ExperimentCancellationToken.Token).ConfigureAwait(false);
                _logger.LogInformation("Finished preloading weather. Took {Time}. Waiting to be consumed...", st.Elapsed);

                await minimumWaitPreloadWaitTime; // Wait for the minium time of 10 seconds before checking if everything is consumed
                await _consumingMonitor.WaitForExchangesToBeConsumedAsync(ExperimentRunnerClientIds.ToArray(), ExperimentCancellationToken.Token);
                minimumWaitPreloadWaitTime = Task.Delay(TimeSpan.FromSeconds(10));
                ExperimentCancellationToken.Token.ThrowIfCancellationRequested();

                st.Restart();

                _flightInjector.SkipFlightsUntil(CurrentExperiment.SimulatedPreloadStartTime, ExperimentCancellationToken.Token);

                if (CurrentExperiment.PreloadAllFlights)
                {
                    await _flightInjector.PublishFlightsUntil(DateTime.MaxValue, CurrentExperiment.Id, _logger, ExperimentCancellationToken.Token).ConfigureAwait(false);
                }
                else
                {
                    await _flightInjector.PublishFlightsUntil(CurrentExperiment.SimulatedPreloadEndTime, CurrentExperiment.Id, _logger, ExperimentCancellationToken.Token).ConfigureAwait(false);
                }
                _logger.LogInformation("Finished preloading flights. Took {Time}", st.Elapsed);

                ExperimentCancellationToken.Token.ThrowIfCancellationRequested();

                _logger.LogInformation("Published all preload-data. Waiting for consumers to consume it all");

                await minimumWaitPreloadWaitTime; // Wait for the minium time of 10 seconds before checking if everything is consumed
                await _consumingMonitor.WaitForExchangesToBeConsumedAsync(ExperimentRunnerClientIds.ToArray(), ExperimentCancellationToken.Token);

                _logger.LogInformation("Preload done");
                OrchestratorState = OrchestratorState.PreloadDone;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error during preload");
                ResetExperimentState();
            }
            finally
            {
                _experimentControllerSemaphore.Release();
                OnExperimentStateChanged?.Invoke();
            }
        }

        public async Task StartExperimentAsync(string newExperimentDescription)
        {
            if (ExperimentRunnerClientIds.Count == 0)
            {
                _logger.LogError("Cannot start experiment because there is no ExperimentRunnerClientIds set");
                return;
            }
            if (CurrentExperiment is null)
            {
                throw new InvalidOperationException("No experiment is currently set.");
            }
            if (_flightInjector is null || _weatherInjector is null)
            {
                throw new InvalidOperationException("FlightInjector and WeatherInjector are not set.");
            }
            if (CheckExperimentRunning())
            {
                throw new InvalidOperationException("An experiment is detected to be running");
            }
            if (OrchestratorState != OrchestratorState.PreloadDone)
            {
                throw new InvalidOperationException("Experiment cannot start because preload is not done.");
            }
            await _experimentControllerSemaphore.WaitAsync().ConfigureAwait(false);

            try
            {
                if (OrchestratorState != OrchestratorState.PreloadDone)
                {
                    return;
                }
                OrchestratorState = OrchestratorState.Starting;

                ExperimentCancellationToken = new CancellationTokenSource();

                CurrentExperimentResult = new ExperimentResult()
                {
                    ExperimentId = CurrentExperiment.Id,
                    ExperimentRunDescription = newExperimentDescription,
                };

                _logger.LogInformation("Starting Experiment with Latency Test");
                var latencyResults = await _latencyTester
                        .GetConsumersAndLatencyAsync(LatencyTestSamples, LatencyTestFrequencyMs)
                        .ConfigureAwait(false);

                _logger.LogInformation("Latency test results: {LatencyTestResults}",
                    string.Join(", ", latencyResults));
                if (ExperimentRunnerClientIds.Except(latencyResults.Select(x => x.ClientId)).Any())
                {
                    throw new InvalidOperationException("Latency test results do not contain results for all experiment runners.");
                }

                // Add Latency results to experiment result
                CurrentExperimentResult.ClientResults = latencyResults.Where(x => ExperimentRunnerClientIds.Contains(x.ClientId))
                    .Select(x => new ExperimentClientResult()
                    {
                        ClientId = x.ClientId,
                        LatencyTest = x,
                        ExperimentResult = CurrentExperimentResult,
                        DataStoreType = x.DataStoreType,
                    }).ToList();

                // Start the experiment clock
                CurrentExperimentResult.UTCStartTime = DateTime.UtcNow;

                // Skip ahead to simulation start
                _flightInjector.SkipFlightsUntil(CurrentExperiment.SimulatedStartTime, ExperimentCancellationToken.Token);
                _weatherInjector.SkipWeatherUntil(CurrentExperiment.SimulatedStartTime, ExperimentCancellationToken.Token);

                await _experimentDataCollector.AddOrUpdateExperimentResultAsync(CurrentExperimentResult);
                await _experimentDataCollector.MonitorExperimentAsync(CurrentExperiment.Id);
                CurrentSimulationTime = CurrentExperiment.SimulatedStartTime;
                OrchestratorState = OrchestratorState.Running;
                ExperimentTask = ExperimentLoop(); // Don't wait
                ExperimentConsumerLagTask = MonitorConsumerLagAsync();
                _experimentChecker.Start();
                _logger.LogInformation("Experiment successfully started!");
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error during experiment start");
                ResetExperimentState();
            }
            finally
            {
                _experimentControllerSemaphore.Release();
                OnExperimentStateChanged?.Invoke();
            }
        }

        public bool CheckExperimentRunning()
        {
            if (ExperimentTask is null)
            {
                return false;
            }
            lock (ExperimentTask)
            {
                if (ExperimentTask is null)
                {
                    return false;
                }

                if (ExperimentTask.IsFaulted)
                {
                    _logger.LogError(ExperimentTask.Exception, "ExperimentTask is faulted: {Exception}", ExperimentTask.Exception);
                    if (CurrentExperimentResult is { } result)
                    {
                        result.ExperimentError = $"ExperimentTask threw exception of type {ExperimentTask.Exception.GetType().FullName}: {ExperimentTask.Exception?.Message}";
                        result.UTCEndTime = DateTime.UtcNow;
                        result.ExperimentSuccess = false;
                    }
                    ResetExperimentState();
                    return false;
                }
                else if (ExperimentTask.IsCompleted)
                {
                    ResetExperimentState();
                    return false;
                }
            }
            return true;
        }

        private void ResetExperimentState()
        {
            // Don't clear the result here!
            ExperimentTask = null;
            ExperimentConsumerLagTask = null;
            ExperimentCancellationToken = new CancellationTokenSource();
            OrchestratorState = OrchestratorState.Idle;
            CurrentLag = new();
            _experimentDataCollector.StopMonitoringExperiment();
            _experimentChecker.Stop();
            _flightInjector?.ResetReader();
            _weatherInjector?.ResetReader();
            OnExperimentStateChanged?.Invoke();
        }

        private SemaphoreSlim _abortSemaphore = new SemaphoreSlim(1, 1);

        public async Task AbortExperimentAsync()
        {
            _logger.LogWarning("Aborting experiment");
            await _abortSemaphore.WaitAsync().ConfigureAwait(false);
            OrchestratorState = OrchestratorState.Aborting;
            try
            {
                if (!ExperimentCancellationToken.IsCancellationRequested)
                {
                    ExperimentCancellationToken.Cancel();
                }
                if (CurrentExperimentResult is { ExperimentSuccess: false, UTCEndTime: null } currentResult)
                {
                    currentResult.ExperimentError = "Experiment aborted at " + DateTime.UtcNow;
                    currentResult.UTCEndTime = DateTime.UtcNow;
                    currentResult.ExperimentSuccess = false;
                    await _experimentDataCollector.AddOrUpdateExperimentResultAsync(CurrentExperimentResult);
                    await _experimentDataCollector.FinishDataCollectionAsync(ExperimentRunnerClientIds);
                }
                if (ExperimentTask is not null)
                {
                    // Will "Join the thread" and wait for completion.
                    try
                    {
                        _logger.LogDebug("Joining the experiment task to wait for exit..");
                        await ExperimentTask.ConfigureAwait(false);
                    }
                    catch (Exception e) when (e is not OperationCanceledException)
                    {
                        _logger.LogError(e, "Experiment threw an error while running.");
                    }
                    ExperimentTask = null;
                }
            }
            finally
            {
                _logger.LogWarning("Experiment aborted successfully!");
                ResetExperimentState();
                _abortSemaphore.Release();
            }
        }

        private async Task ExperimentLoop()
        {
            if (CurrentExperiment is null
                || CurrentExperimentResult is null
                || CurrentSimulationTime is null
                || _flightInjector is null
                || _weatherInjector is null)
            {
                _logger.LogWarning("Experiment Loop is called without a valid experiment, experiment result or simulation time.");
                return;
            }
            while (!ExperimentCancellationToken.IsCancellationRequested)
            {
                // Update simulation time
                if (CurrentExperiment.TimeScale > 0)
                {
                    LastUpdateTime ??= DateTime.UtcNow;
                    var now = DateTime.UtcNow;
                    var timeDiff = now - LastUpdateTime.Value;
                    CurrentSimulationTime = CurrentSimulationTime.Value.Add(timeDiff * CurrentExperiment.TimeScale);
                    LastUpdateTime = now;
                }
                else
                {
                    // Run as fast as possible
                    CurrentSimulationTime = CurrentSimulationTime.Value.Add(TimeSpan.FromMinutes(1));
                }

                if (CurrentSimulationTime >= CurrentExperiment.SimulatedEndTime)
                {
                    _logger.LogInformation("Simulation Time done. Waiting for consumers to consume the rest of the data.");
                    await _consumingMonitor.WaitForExchangesToBeConsumedAsync(ExperimentRunnerClientIds.ToArray(), ExperimentCancellationToken.Token);
                    CurrentExperimentResult.UTCEndTime = DateTime.UtcNow;
                    CurrentExperimentResult.ExperimentSuccess = true;
                    await _experimentDataCollector.AddOrUpdateExperimentResultAsync(CurrentExperimentResult);
                    await Task.Delay(TimeSpan.FromSeconds(15)); // Simply to ensure we get the rest of the recalculaitons if some of them were to be in the last dataset
                    await _experimentDataCollector.FinishDataCollectionAsync(ExperimentRunnerClientIds);
                    _logger.LogInformation("Experiment {Id} ended successfully.", CurrentExperiment.Id);
                    ResetExperimentState();
                    return;
                }

                OnExperimentStateChanged?.Invoke();

                // Inject weather and flights up to CurrentSimulationTime

                var weatherTask = _weatherInjector.PublishWeatherUntil(CurrentSimulationTime.Value, CurrentExperiment.Id, _logger, ExperimentCancellationToken.Token);

                if (!CurrentExperiment.PreloadAllFlights)
                {
                    var flightTask = _flightInjector.PublishFlightsUntil(CurrentSimulationTime.Value, CurrentExperiment.Id, _logger, ExperimentCancellationToken.Token);
                    // Will inject flights and weather concurrently
                    await Task.WhenAll(weatherTask, flightTask).ConfigureAwait(false);
                }
                else
                {
                    await weatherTask.ConfigureAwait(false);
                }

                if (ExperimentLoopIntervalMs > 0 && CurrentExperiment.TimeScale > 0)
                {
                    await Task.Delay(ExperimentLoopIntervalMs, ExperimentCancellationToken.Token).ConfigureAwait(false);
                }
            }
            _logger.LogWarning("ExperimentLoop stopped: Cancelled={Cancelled}", ExperimentCancellationToken.IsCancellationRequested);
        }

        private async Task MonitorConsumerLagAsync()
        {
            while (!ExperimentCancellationToken.IsCancellationRequested
                && ExperimentTask?.IsCompleted == false // Don't monitor if the experiment is not running
                && CurrentExperimentResult is not null)
            {
                var experimentLag = await _consumingMonitor.GetMessageLagAsync(ExperimentRunnerClientIds.ToArray(), ExperimentCancellationToken.Token).ConfigureAwait(false);
                CurrentLag = experimentLag;
                foreach (var (clientId, lag) in experimentLag)
                {
                    if (CurrentExperimentResult.ClientResults.FirstOrDefault(x => x.ClientId == clientId) is { } result)
                    {
                        result.MaxFlightConsumerLag = Math.Max(result.MaxFlightConsumerLag, lag.flightLag);
                        result.MaxWeatherConsumerLag = Math.Max(result.MaxWeatherConsumerLag, lag.weatherLag);
                    }
                }

                if (CurrentSimulationTime >= CurrentExperiment!.SimulatedEndTime)
                {
                    // When the experiment is done over, start ticking the update as the lag does down.
                    OnExperimentStateChanged?.Invoke();
                }

                await Task.Delay(200, ExperimentCancellationToken.Token).ConfigureAwait(false);
            }
        }

        private async Task<(bool success, List<SystemMessage> responses)> SendSystemMessageAndWaitForResponseAsync(SystemMessage messageToSend, SystemMessageType eventToWaitFor, TimeSpan timeout)
        {
            var waitingFor = new HashSet<string>(ExperimentRunnerClientIds);
            var responses = new List<SystemMessage>();
            Func<SystemMessage, Task> listener = (systemMessage) =>
            {
                if (systemMessage.MessageType == eventToWaitFor
                    && systemMessage.Message == messageToSend.Message)
                {
                    responses.Add(systemMessage);
                    waitingFor.Remove(systemMessage.Source);
                }
                return Task.CompletedTask;
            };
            _eventBus.SubscribeToSystemEvent(listener);
            try
            {
                await _eventBus.PublishSystemMessage(messageToSend).ConfigureAwait(false);
                using var cts = new CancellationTokenSource();
                cts.CancelAfter(timeout);
                while (!cts.IsCancellationRequested)
                {
                    if (waitingFor.Count == 0)
                    {
                        return (true, responses);
                    }
                    await Task.Delay(100).ConfigureAwait(false);
                }
            }
            finally
            {
                _eventBus.UnSubscribeToSystemEvent(listener);
            }
            return (false, responses);
        }

        public void Dispose()
        {
            ExperimentCancellationToken.Cancel();
            ExperimentCancellationToken.Dispose();
            _experimentChecker.Stop();
            _experimentChecker.Dispose();
            _logger.OnLog -= OnLog;
        }
    }
}
