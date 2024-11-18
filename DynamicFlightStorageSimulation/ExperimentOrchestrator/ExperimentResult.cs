namespace DynamicFlightStorageSimulation.ExperimentOrchestrator
{
    public class ExperimentResult
    {
        public required Experiment Experiment { get; init; }
        public DateTime? ExperimentStarted { get; set; }
        public DateTime? ExperimentEnded { get; set; }
        public string? ExperimentError { get; set; }
        public bool ExperimentSuccess { get; set; }
        public Dictionary<string, ExperimentClientResult> ClientResults { get; set; } = new();
    }
}
