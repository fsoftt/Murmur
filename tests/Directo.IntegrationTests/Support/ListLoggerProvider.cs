using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Directo.IntegrationTests.Support;

/// <summary>Captures log lines in memory so a failing test can print what each device did.</summary>
public sealed class ListLoggerProvider(string device) : ILoggerProvider, ILoggerFactory
{
    public ConcurrentQueue<string> Lines { get; } = new();

    public string Device { get; } = device;

    public ILogger CreateLogger(string categoryName) => new ListLogger(this, categoryName[(categoryName.LastIndexOf('.') + 1)..]);

    public void AddProvider(ILoggerProvider provider)
    {
    }

    public void Dispose()
    {
    }

    private sealed class ListLogger(ListLoggerProvider owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Debug;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            owner.Lines.Enqueue($"{DateTime.UtcNow:HH:mm:ss.fff} [{owner.Device}] {category}: {formatter(state, exception)}");
    }
}
