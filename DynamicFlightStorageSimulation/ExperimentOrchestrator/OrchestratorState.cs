namespace DynamicFlightStorageSimulation.ExperimentOrchestrator
{
    public enum OrchestratorState
    {
        Idle = 0,
        Preloading = 1,
        PreloadDone = 2,
        Starting = 3,
        Running = 4,
        Aborting = 5
    }
}
