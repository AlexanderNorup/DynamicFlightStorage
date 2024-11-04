using DynamicFlightStorageDTOs;
using DynamicFlightStorageSimulation.Events;
using Microsoft.Extensions.Logging;
using static DynamicFlightStorageDTOs.SystemMessage;
using static DynamicFlightStorageSimulation.SimulationEventBus;

namespace DynamicFlightStorageSimulation.ExperimentOrchestrator
{
    public class Orchestrator : IDisposable
    {
        public const int LatencyTestSamples = 20;
        public const int LatencyTestFrequencyMs = 100;

        private SimulationEventBus _eventBus;
        private LatencyTester _latencyTester;
        private WeatherInjector _weatherInjector;
        private FlightInjector _flightInjector;
        private System.Timers.Timer _experimentLoopTimer;
        private ILogger<Orchestrator> _logger;
        public Orchestrator(SimulationEventBus eventBus, LatencyTester latencyTester, WeatherInjector weatherInjector, FlightInjector flightInjector, ILogger<Orchestrator> logger)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _latencyTester = latencyTester ?? throw new ArgumentNullException(nameof(latencyTester));
            _weatherInjector = weatherInjector ?? throw new ArgumentNullException(nameof(weatherInjector));
            _flightInjector = flightInjector ?? throw new ArgumentNullException(nameof(flightInjector));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _experimentLoopTimer = new System.Timers.Timer
            {
                Interval = 100 // Milliseconds
            };
            _experimentLoopTimer.Elapsed += async (s, e) => await ExperimentLoop().ConfigureAwait(false);
        }
        public HashSet<string> ExperimentRunnerClientIds { get; } = new HashSet<string>();

        public Experiment? CurrentExperiment { get; private set; }
        public ExperimentResult? CurrentExperimentResult { get; private set; }
        public bool PreloadDone { get; private set; }

        public DateTime? CurrentSimulationTime { get; private set; }
        public DateTime? LastUpdateTime { get; private set; }

        public void SetExperiment(Experiment experiment)
        {
            if (CurrentExperiment is not null)
            {
                throw new InvalidOperationException("An experiment is already running.");
            }
            PreloadDone = false;
            CurrentExperiment = experiment;
        }

        public async Task RunExperimentPreloadAsync()
        {
            if(ExperimentRunnerClientIds.Count == 0)
            {
                _logger.LogError("Cannot start preload because there is no ExperimentRunnerClientIds set");
                return;
            }
            if (CurrentExperiment is null)
            {
                throw new InvalidOperationException("No experiment is currently set.");
            }

            _logger.LogInformation("Doing Experiment Preload");
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

            await _weatherInjector.PublishWeatherUntil(CurrentExperiment.SimulatedPreloadEndTime).ConfigureAwait(false);

            if (CurrentExperiment.PreloadAllFlights)
            {
                await _flightInjector.PublishFlightsUntil(DateTime.MaxValue).ConfigureAwait(false);
            }
            else
            {
                await _flightInjector.PublishFlightsUntil(CurrentExperiment.SimulatedPreloadEndTime).ConfigureAwait(false);
            }

            //TODO: Check Queue if clients are done with preloading
            // Untill then we just wait a bit
            await Task.Delay(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            PreloadDone = true;
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
            if (!PreloadDone)
            {
                await RunExperimentPreloadAsync().ConfigureAwait(false);
            }

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
            _experimentLoopTimer.Start();
        }

        public void AbortExperiment()
        {
            _experimentLoopTimer.Stop();
            if (CurrentExperimentResult is { } currentResult)
            {
                currentResult.ExperimentError = "Experiment aborted at " + DateTime.UtcNow;
                return;
            }
        }

        public async Task ExperimentLoop()
        {
            if (CurrentExperiment is null || CurrentExperimentResult is null || CurrentSimulationTime is null)
            {
                _logger.LogWarning("Experiment Loop is called without a valid experiment, experiment result or simulation time.");
                return;
            }

            // Update simulation time
            LastUpdateTime ??= DateTime.UtcNow;
            var now = DateTime.UtcNow;
            var timeDiff = now - LastUpdateTime.Value;
            CurrentSimulationTime = CurrentSimulationTime.Value.Add(timeDiff);
            LastUpdateTime = now;

            if (CurrentSimulationTime >= CurrentExperiment.SimulatedEndTime)
            {
                _experimentLoopTimer.Stop();
                CurrentExperimentResult.ExperimentEnded = DateTime.UtcNow;
                CurrentExperimentResult.ExperimentSuccess = true;
                _logger.LogInformation("Experiment {Id} ended successfully.", CurrentExperiment.Id);
                return;
            }

            // Inject weather and flights up to CurrentSimulationTime

            var weatherTask = _weatherInjector.PublishWeatherUntil(CurrentSimulationTime.Value);

            if (!CurrentExperiment.PreloadAllFlights)
            {
                var flightTask = _flightInjector.PublishFlightsUntil(CurrentSimulationTime.Value);
                // Will inject flights and weather concurrently
                await Task.WhenAll(weatherTask, flightTask).ConfigureAwait(false);
            }
            else
            {
                await weatherTask.ConfigureAwait(false);
            }
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
            _experimentLoopTimer.Dispose();
        }
    }
}
