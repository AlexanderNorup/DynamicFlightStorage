using Microsoft.Extensions.Logging;

namespace DynamicFlightStorageSimulation.Utilities
{
    public record LogEntry(LogLevel LogLevel, string Message, Exception? Exception)
    {
        public DateTime Timestamp { get; init; } = DateTime.Now;
    }
}
