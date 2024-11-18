namespace DynamicFlightStorageSimulation.ExperimentOrchestrator
{
    public class ExperimentClientResult
    {
        public required string ClientId { get; set; }
        public LatencyTestResult? LatencyTest { get; set; }
        public int MaxFlightConsumerLag { get; set; } = -1;
        public int MaxWeatherConsumerLag { get; set; } = -1;
    }
}