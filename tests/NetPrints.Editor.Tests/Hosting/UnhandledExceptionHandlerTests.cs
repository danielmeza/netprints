using Microsoft.Extensions.Logging;
using NetPrints.Editor.Hosting.Avalonia;

namespace NetPrints.Editor.Tests.Hosting;

/// <summary>Records every log entry, for asserting on event ids without a real logging backend.</summary>
public sealed class CollectingLogger<T> : ILogger<T>
{
    public sealed record Entry(LogLevel Level, EventId EventId, string Message, Exception? Exception);

    public List<Entry> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Entries.Add(new Entry(logLevel, eventId, formatter(state, exception), exception));
}

/// <summary>ED-T12: <see cref="UnhandledExceptionHandler"/> logs 1001/1002 and still shows the dialog.</summary>
public class UnhandledExceptionHandlerTests
{
    [Fact]
    public void UnhandledUiExceptionLogs1001AndShowsTheDialog()
    {
        var dialogs = new FakeDialogs();
        var dispatcher = new InlineDispatcher();
        var logger = new CollectingLogger<UnhandledExceptionHandler>();
        using var handler = new UnhandledExceptionHandler(dialogs, dispatcher, logger);
        var exception = new InvalidOperationException("boom");

        handler.ReportUnhandledUiException(exception);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(1001, entry.EventId.Id);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Same(exception, entry.Exception);

        var shown = Assert.Single(dialogs.Errors);
        Assert.Contains("boom", shown.Message);
    }

    [Fact]
    public void UnobservedTaskExceptionLogs1002AndShowsTheDialog()
    {
        var dialogs = new FakeDialogs();
        var dispatcher = new InlineDispatcher();
        var logger = new CollectingLogger<UnhandledExceptionHandler>();
        using var handler = new UnhandledExceptionHandler(dialogs, dispatcher, logger);
        var exception = new AggregateException(new InvalidOperationException("kaboom"));

        handler.ReportUnobservedTaskException(exception);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(1002, entry.EventId.Id);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Same(exception, entry.Exception);

        var shown = Assert.Single(dialogs.Errors);
        Assert.Contains("kaboom", shown.Message);
    }

    [Fact]
    public void AnUnobservedTaskExceptionAlreadyReportedAsAUiExceptionIsNotShownTwice()
    {
        var dialogs = new FakeDialogs();
        var dispatcher = new InlineDispatcher();
        var logger = new CollectingLogger<UnhandledExceptionHandler>();
        using var handler = new UnhandledExceptionHandler(dialogs, dispatcher, logger);
        var inner = new InvalidOperationException("already shown");

        handler.ReportUnhandledUiException(inner);
        Assert.Single(dialogs.Errors);

        handler.ReportUnobservedTaskException(new AggregateException(inner));

        // Still logged (1001 then 1002), but the dialog is not shown a second time for the same exception.
        Assert.Equal(2, logger.Entries.Count);
        Assert.Single(dialogs.Errors);
    }
}
