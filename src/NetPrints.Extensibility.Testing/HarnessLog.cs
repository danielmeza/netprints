using Microsoft.Extensions.Logging;

namespace NetPrints.Extensibility.Testing;

/// <summary>One message the host logged while a harness loaded or disposed extensions.</summary>
/// <param name="Level">The log level.</param>
/// <param name="EventId">The event id of the message.</param>
/// <param name="Category">The logger category.</param>
/// <param name="Message">The formatted message, with the exception text appended when there is one.</param>
public sealed record HarnessLogEntry(LogLevel Level, EventId EventId, string Category, string Message);

/// <summary>The warnings and errors the host logged for a harness, kept after the harness is disposed.</summary>
public sealed class HarnessLog
{
    private readonly List<HarnessLogEntry> entries = [];

    /// <summary>Gets a snapshot of the messages of warning level and above logged so far, including the ones from disposal.</summary>
    public IReadOnlyList<HarnessLogEntry> Entries
    {
        get
        {
            lock (entries)
            {
                return [.. entries];
            }
        }
    }

    internal ILoggerFactory CreateFactory() => new Factory(entries);

    private sealed class Factory(List<HarnessLogEntry> entries) : ILoggerFactory
    {
        public void AddProvider(ILoggerProvider provider)
        {
        }

        public ILogger CreateLogger(string categoryName) => new Logger(categoryName, entries);

        public void Dispose()
        {
        }
    }

    private sealed class Logger(string category, List<HarnessLogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            if (!IsEnabled(logLevel))
            {
                return;
            }

            string message = formatter(state, exception);
            if (exception is not null)
            {
                message += ": " + exception.GetType().Name + ": " + exception.Message;
            }

            lock (entries)
            {
                entries.Add(new HarnessLogEntry(logLevel, eventId, category, message));
            }
        }
    }
}
