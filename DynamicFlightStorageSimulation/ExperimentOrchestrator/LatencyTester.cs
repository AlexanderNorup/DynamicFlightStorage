using DynamicFlightStorageDTOs;
using DynamicFlightStorageSimulation.Utilities;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace DynamicFlightStorageSimulation.ExperimentOrchestrator
{
    public class LatencyTester : IDisposable
    {
        private ILogger<LatencyTester>? _logger;
        private SimulationEventBus _simulationEventBus;
        private bool doingLatencyExperiment = false;
        private ConcurrentBag<(SystemMessage message, DateTime messageRecieved)> latencyExperimentBag = new();
        private bool disposedValue;

        public LatencyTester(SimulationEventBus simulationEventBus, ILogger<LatencyTester> logger)
        {
            _logger = logger;
            _simulationEventBus = simulationEventBus ?? throw new ArgumentNullException(nameof(simulationEventBus));
            _simulationEventBus.SubscribeToSystemEvent(OnSystemMessage);
        }

        public bool LatencyExperimentRunning => doingLatencyExperiment;

        public async Task<List<LatencyTestResult>> GetConsumersAndLatencyAsync(int samplePoints, int sampleDelayMs, CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(samplePoints, 1);
            ArgumentOutOfRangeException.ThrowIfLessThan(sampleDelayMs, 1);
            if (doingLatencyExperiment)
            {
                throw new InvalidOperationException("Latency experiment is already running.");
            }
            doingLatencyExperiment = true;
            try
            {
                latencyExperimentBag.Clear();
                var experimentId = Guid.NewGuid().ToString();
                _logger?.LogInformation("Starting latency experiment {ExperimentId} with {SamplePoints} samples with a delay of {SampleDelay}.",
                    experimentId, samplePoints, sampleDelayMs);

                for (int i = 0; i < samplePoints; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await _simulationEventBus.PublishSystemMessage(new SystemMessage()
                    {
                        Message = $"{experimentId}_{i}",
                        MessageType = SystemMessage.SystemMessageType.LatencyRequest,
                        Source = _simulationEventBus.ClientId,
                        TimeStamp = DateTime.UtcNow
                    }).ConfigureAwait(false);
                    _logger?.LogDebug("Experiment progress {Sent}/{Total}",
                        i, samplePoints);
                    await Task.Delay(sampleDelayMs, cancellationToken).ConfigureAwait(false);
                }

                cancellationToken.ThrowIfCancellationRequested();
                _logger?.LogInformation("Waiting for responses for {ExperimentId}.", experimentId);
                await Task.Delay(500, cancellationToken).ConfigureAwait(false);

                var responses = latencyExperimentBag.ToList();
                _logger?.LogInformation("Got {Responses} responses.", responses.Count);

                var experimentResults = new List<LatencyTestResult>();
                foreach (var group in responses.GroupBy(x => x.message.Source))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var clientId = group.Key;
                    bool success = true;
                    var clientResponses = group.OrderBy(x => x.message.TimeStamp).ToList();
                    if (clientResponses.Count != samplePoints)
                    {
                        _logger?.LogError("Client {ClientId} did not respond to all messages. Only got {MessagesRecieved}/{MessagesExpected}",
                            clientId, clientResponses.Count, samplePoints);
                        success = false;
                    }

                    // Check if all messages were recieved and in the right order.
                    int i = 0;
                    var latencyForClient = new List<double>(clientResponses.Count);
                    foreach (var (message, timestamp) in clientResponses)
                    {
                        var expectedMessage = $"{experimentId}_{i}";
                        if (message.Message != expectedMessage)
                        {
                            _logger?.LogError("Client {ClientId} did not respond to message {MessageId}. Expected {ExpectedMessage} but got {ActualMessage}",
                                clientId, i, expectedMessage, message.Message);
                            success = false;
                        }

                        var latency = (timestamp - message.TimeStamp).TotalMilliseconds;
                        latencyForClient.Add(latency);
                        i++;
                    }

                    experimentResults.Add(new LatencyTestResult(clientId,
                        success,
                        samplePoints,
                        sampleDelayMs,
                        latencyForClient.Average(),
                        latencyForClient.Median(),
                        latencyForClient.StdDev())
                    );
                }
                return experimentResults;
            }
            finally
            {
                doingLatencyExperiment = false;
            }
        }

        private Task OnSystemMessage(SystemMessage message)
        {
            if (message.MessageType is not SystemMessage.SystemMessageType.LatencyResponse)
            {
                return Task.CompletedTask;
            }

            latencyExperimentBag.Add((message, DateTime.UtcNow));
            return Task.CompletedTask;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _simulationEventBus.UnSubscribeToSystemEvent(OnSystemMessage);
                }
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
