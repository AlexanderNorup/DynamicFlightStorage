using Microsoft.Extensions.Logging;

namespace DynamicFlightStorageSimulation.Utilities
{
    public class EventLogger<T> : ILogger<T> where T : class
    {
        private readonly ILogger<T>? _logger;
        public EventLogger(ILogger<T>? logger = null)
        {
            _logger = logger;
        }

        public event OnLogEvent? OnLog;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            _logger?.Log(logLevel, eventId, state, exception, formatter);
            OnLog?.Invoke(new LogEntry(logLevel, formatter(state, exception), exception));
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return _logger?.BeginScope(state) ?? default!;
        }
    }
}
