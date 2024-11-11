using Microsoft.Extensions.Logging;
using System.Text.Json.Serialization;

namespace DynamicFlightStorageSimulation.Utilities
{
    public record LogEntry(LogLevel LogLevel, string Message, [property: JsonIgnore] Exception? Exception)
    {
        public DateTime Timestamp { get; init; } = DateTime.Now;
        public string? ExceptionMessage => Exception?.Message;
    }
}
