using DynamicFlightStorageDTOs;
using Microsoft.Extensions.Logging;
using static DynamicFlightStorageDTOs.SystemMessage;
using static DynamicFlightStorageSimulation.SimulationEventBus;

namespace DynamicFlightStorageSimulation.ExperimentOrchestrator
{
    public class Orchestrator : IDisposable
    {
        public const int LatencyTestSamples = 20;
        public const int LatencyTestFrequencyMs = 100;
        public const int ExperimentLoopIntervalMs = 100;

        private SimulationEventBus _eventBus;
        private LatencyTester _latencyTester;
        private WeatherInjector _weatherInjector;
        private FlightInjector _flightInjector;
        private ILogger<Orchestrator> _logger;
        private System.Timers.Timer _experimentChecker;
        private SemaphoreSlim _experimentControllerSemaphore = new SemaphoreSlim(1, 1);
        public Orchestrator(SimulationEventBus eventBus, LatencyTester latencyTester, WeatherInjector weatherInjector, FlightInjector flightInjector, ILogger<Orchestrator> logger)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _latencyTester = latencyTester ?? throw new ArgumentNullException(nameof(latencyTester));
            _weatherInjector = weatherInjector ?? throw new ArgumentNullException(nameof(weatherInjector));
            _flightInjector = flightInjector ?? throw new ArgumentNullException(nameof(flightInjector));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _experimentChecker = new System.Timers.Timer(1000);
            _experimentChecker.Elapsed += (s, e) => CheckExperimentRunning();
            _experimentChecker.AutoReset = true;
        }
        public HashSet<string> ExperimentRunnerClientIds { get; } = new HashSet<string>();

        public Experiment? CurrentExperiment { get; private set; }
        public Task? ExperimentTask { get; private set; }
        public ExperimentResult? CurrentExperimentResult { get; private set; }

        public OrchestratorState OrchestratorState { get; private set; } = OrchestratorState.Idle;

        public DateTime? CurrentSimulationTime { get; private set; }
        public DateTime? LastUpdateTime { get; private set; }
        public CancellationTokenSource ExperimentCancellationToken { get; private set; } = new();

        public void SetExperiment(Experiment experiment)
        {
            _ = experiment ?? throw new ArgumentNullException(nameof(experiment));
            if (OrchestratorState != OrchestratorState.Idle
                && !CheckExperimentRunning())
            {
                throw new InvalidOperationException($"An experiment can only be set when the state is {OrchestratorState.Idle}.");
            }
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

                ExperimentCancellationToken = new CancellationTokenSource();
                var result = await SendSystemMessageAndWaitForResponseAsync(new SystemMessage()
                {
                    Message = CurrentExperiment.Id,
                    MessageType = SystemMessageType.NewExperiment,
                    Source = _eventBus.ClientId
                }, SystemMessageType.NewExperimentReady, TimeSpan.FromSeconds(10))
                    .ConfigureAwait(false);

                if (!result.success)
                {
                    throw new InvalidOperationException("One or more experiment runners did not respond in time.\n" +
                        $"Missing response(s) from {string.Join(",", ExperimentRunnerClientIds.Except(result.responses.Select(x => x.Source)))}");
                }

                // Do preload here.

                await _weatherInjector.PublishWeatherUntil(CurrentExperiment.SimulatedPreloadEndTime, ExperimentCancellationToken.Token).ConfigureAwait(false);

                if (CurrentExperiment.PreloadAllFlights)
                {
                    await _flightInjector.PublishFlightsUntil(DateTime.MaxValue, ExperimentCancellationToken.Token).ConfigureAwait(false);
                }
                else
                {
                    await _flightInjector.PublishFlightsUntil(CurrentExperiment.SimulatedPreloadEndTime, ExperimentCancellationToken.Token).ConfigureAwait(false);
                }

                ExperimentCancellationToken.Token.ThrowIfCancellationRequested();

                //TODO: Check Queue if clients are done with preloading
                // Untill then we just wait a bit
                _logger.LogInformation("Published all preload-data. Waiting for consumers to consume it all");
                await Task.Delay(TimeSpan.FromSeconds(10), ExperimentCancellationToken.Token).ConfigureAwait(false);
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
            }
        }

        public async Task StartExperimentAsync()
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

                ExperimentCancellationToken = new CancellationTokenSource();

                CurrentExperimentResult = new ExperimentResult()
                {
                    Experiment = CurrentExperiment
                };

                _logger.LogInformation("Starting Experiment with Latency Test");
                var latencyResults = await _latencyTester
                        .GetConsumersAndLatencyAsync(LatencyTestSamples, LatencyTestFrequencyMs)
                        .ConfigureAwait(false);

                _logger.LogInformation("Latency test results: {LatencyTestResults}",
                    string.Join(", ", latencyResults));
                if (ExperimentRunnerClientIds.Except(latencyResults.Select(x => x.Clientid)).Any())
                {
                    throw new InvalidOperationException("Latency test results do not contain results for all experiment runners.");
                }
                // Add Latency results to experiment result
                CurrentExperimentResult.LatencyTestResults
                    .AddRange(latencyResults.Where(x => ExperimentRunnerClientIds.Contains(x.Clientid)));

                // Start the experiment clock
                CurrentExperimentResult.ExperimentStarted = DateTime.UtcNow;
                CurrentSimulationTime = CurrentExperiment.SimulatedStartTime;
                OrchestratorState = OrchestratorState.Running;
                ExperimentTask = ExperimentLoop(); // Don't wait
                _experimentChecker.Start();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error during experiment start");
                ResetExperimentState();
            }
            finally
            {
                _experimentControllerSemaphore.Release();
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
                        result.ExperimentEnded = DateTime.UtcNow;
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
            ExperimentCancellationToken = new CancellationTokenSource();
            OrchestratorState = OrchestratorState.Idle;
            _experimentChecker.Stop();
        }

        private SemaphoreSlim _abortSemaphore = new SemaphoreSlim(1, 1);
        public async Task AbortExperimentAsync()
        {
            await _abortSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!ExperimentCancellationToken.IsCancellationRequested)
                {
                    ExperimentCancellationToken.Cancel();
                }
                if (CurrentExperimentResult is { ExperimentSuccess: false, ExperimentEnded: null } currentResult)
                {
                    currentResult.ExperimentError = "Experiment aborted at " + DateTime.UtcNow;
                    currentResult.ExperimentEnded = DateTime.UtcNow;
                    currentResult.ExperimentSuccess = false;
                }
                if (ExperimentTask is not null)
                {
                    // Will "Join the thread" and wait for completion.
                    try
                    {
                        await ExperimentTask.ConfigureAwait(false);
                    }
                    catch (Exception e) when (e is not OperationCanceledException)
                    {
                        _logger.LogError(e, "Experiment threw an error while running.");
                    }
                    ExperimentTask = null;
                }
                ResetExperimentState();
            }
            finally
            {
                _abortSemaphore.Release();
            }
        }

        public async Task ExperimentLoop()
        {
            if (CurrentExperiment is null || CurrentExperimentResult is null || CurrentSimulationTime is null)
            {
                _logger.LogWarning("Experiment Loop is called without a valid experiment, experiment result or simulation time.");
                return;
            }
            while (!ExperimentCancellationToken.IsCancellationRequested)
            {
                // Update simulation time
                LastUpdateTime ??= DateTime.UtcNow;
                var now = DateTime.UtcNow;
                var timeDiff = now - LastUpdateTime.Value;
                CurrentSimulationTime = CurrentSimulationTime.Value.Add(timeDiff);
                LastUpdateTime = now;

                if (CurrentSimulationTime >= CurrentExperiment.SimulatedEndTime)
                {
                    CurrentExperimentResult.ExperimentEnded = DateTime.UtcNow;
                    CurrentExperimentResult.ExperimentSuccess = true;
                    _logger.LogInformation("Experiment {Id} ended successfully.", CurrentExperiment.Id);
                    ResetExperimentState();
                    return;
                }

                // Inject weather and flights up to CurrentSimulationTime

                var weatherTask = _weatherInjector.PublishWeatherUntil(CurrentSimulationTime.Value, ExperimentCancellationToken.Token);

                if (!CurrentExperiment.PreloadAllFlights)
                {
                    var flightTask = _flightInjector.PublishFlightsUntil(CurrentSimulationTime.Value, ExperimentCancellationToken.Token);
                    // Will inject flights and weather concurrently
                    await Task.WhenAll(weatherTask, flightTask).ConfigureAwait(false);
                }
                else
                {
                    await weatherTask.ConfigureAwait(false);
                }

                await Task.Delay(ExperimentLoopIntervalMs, ExperimentCancellationToken.Token).ConfigureAwait(false);
            }
            _logger.LogWarning("ExperimentLoop stopped: Cancelled={Cancelled}", ExperimentCancellationToken.IsCancellationRequested);
        }

        private async Task<(bool success, List<SystemMessage> responses)> SendSystemMessageAndWaitForResponseAsync(SystemMessage messageToSend, SystemMessageType eventToWaitFor, TimeSpan timeout)
        {
            var waitingFor = new HashSet<string>(ExperimentRunnerClientIds);
            var responses = new List<SystemMessage>();
            SystemMessageEventHandler listener = (e) =>
            {
                if (e.SystemMessage.MessageType == eventToWaitFor
                    && e.SystemMessage.Message == messageToSend.Message)
                {
                    responses.Add(e.SystemMessage);
                    waitingFor.Remove(e.SystemMessage.Source);
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
        }
    }
}
