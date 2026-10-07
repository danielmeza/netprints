using Microsoft.Extensions.Logging;

namespace NetPrints.Editor.UITests.Hosting;

/// <summary>Collects the event ids of everything logged.</summary>
internal sealed class RecordingLoggerFactory : ILoggerFactory
{
    private readonly List<int> ids = [];

    public IReadOnlyList<int> Ids
    {
        get
        {
            lock (ids)
            {
                return [.. ids];
            }
        }
    }

    public void AddProvider(ILoggerProvider provider)
    {
    }

    public ILogger CreateLogger(string categoryName) => new Recorder(ids);

    public void Dispose()
    {
    }

    private sealed class Recorder(List<int> ids) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (ids)
            {
                ids.Add(eventId.Id);
            }
        }
    }
}
