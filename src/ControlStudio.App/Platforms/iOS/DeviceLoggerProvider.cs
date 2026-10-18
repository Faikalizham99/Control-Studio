using Microsoft.Extensions.Logging;

namespace ControlStudio.App.Platforms.iOS;

public sealed class DeviceLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new DeviceLogger(categoryName);
    public void Dispose() { }

    private sealed class DeviceLogger(string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => level >= LogLevel.Information;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(level))
                Console.WriteLine("[{0}] {1} Event={2} {3} ErrorType={4}", level, category,
                    eventId.Id, formatter(state, null), exception?.GetType().Name ?? "none");
        }
    }
}
