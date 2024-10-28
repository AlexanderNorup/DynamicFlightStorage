namespace DynamicFlightStorageSimulation
{
    public record LatencyTestResult(string Clientid,
        bool Success,
        int SamplePoints,
        int SampleDelayMs,
        double AverageLatencyMs,
        double MedianLatencyMs,
        double StdDeviationLatency)
    {
    }
}
