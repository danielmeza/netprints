using Avalonia.Logging;
using Microsoft.Extensions.Logging;
using NetPrints.Desktop;

namespace NetPrints.Editor.Tests.Desktop;

/// <summary>
/// Batch D1 (owner-reported log noise): the sink used to leave message-template placeholders
/// (<c>{Time}</c>) unrendered and forward every Avalonia area at whatever level the process-wide
/// logger was set to, including a layout pass logged at Information on every open graph.
/// </summary>
public class AvaloniaLogSinkTests
{
    private sealed class RecordingLogger : ILogger
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];
        public LogLevel MinimumLevel { get; set; } = LogLevel.Information;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= MinimumLevel;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }

    private sealed class RecordingLoggerFactory : ILoggerFactory
    {
        public Dictionary<string, RecordingLogger> Loggers { get; } = new(StringComparer.Ordinal);
        public LogLevel MinimumLevel { get; set; } = LogLevel.Information;

        public ILogger CreateLogger(string categoryName)
        {
            if (!Loggers.TryGetValue(categoryName, out var logger))
            {
                logger = new RecordingLogger { MinimumLevel = MinimumLevel };
                Loggers[categoryName] = logger;
            }

            return logger;
        }

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public void Dispose()
        {
        }
    }

    [Fact]
    public void RendersMessageTemplatePlaceholdersInsteadOfAppendingRawValues()
    {
        var factory = new RecordingLoggerFactory();
        var sink = new AvaloniaLogSink(factory);

        // "Layout" is a chatty area (floored to Warning without an explicit level): use Warning here
        // so this test only exercises formatting, not the floor.
        sink.Log(LogEventLevel.Warning, "Layout", null, "Layout pass finished in {Time}", TimeSpan.Parse("00:00:00.0338039"));

        string message = Assert.Single(factory.Loggers["Avalonia.Layout"].Entries).Message;
        Assert.DoesNotContain("{Time}", message, StringComparison.Ordinal);
        Assert.Contains("00:00:00.0338039", message, StringComparison.Ordinal);
    }

    [Fact]
    public void RendersEveryPlaceholderInOrder()
    {
        var factory = new RecordingLoggerFactory();
        var sink = new AvaloniaLogSink(factory);

        sink.Log(LogEventLevel.Warning, "Layout", null, "Started layout pass. To measure: {Measure} To arrange: {Arrange}", 7, 0);

        string message = Assert.Single(factory.Loggers["Avalonia.Layout"].Entries).Message;
        Assert.DoesNotContain("{Measure}", message, StringComparison.Ordinal);
        Assert.DoesNotContain("{Arrange}", message, StringComparison.Ordinal);
        Assert.Contains("To measure: 7 To arrange: 0", message, StringComparison.Ordinal);
    }

    [Fact]
    public void ChattyAreasAreFlooredToWarningWithoutAnExplicitLevel()
    {
        var factory = new RecordingLoggerFactory();
        var sink = new AvaloniaLogSink(factory, explicitMinimumLevel: null);

        Assert.False(sink.IsEnabled(LogEventLevel.Information, "Layout"));
        Assert.False(sink.IsEnabled(LogEventLevel.Information, "Visual"));
        Assert.True(sink.IsEnabled(LogEventLevel.Warning, "Layout"));

        // Areas that are not chatty keep the logger factory's own configured minimum.
        Assert.True(sink.IsEnabled(LogEventLevel.Information, "Binding"));
    }

    [Fact]
    public void AnExplicitLevelOverridesTheChattyAreaFloor()
    {
        var factory = new RecordingLoggerFactory { MinimumLevel = LogLevel.Information };
        var sink = new AvaloniaLogSink(factory, explicitMinimumLevel: LogLevel.Information);

        Assert.True(sink.IsEnabled(LogEventLevel.Information, "Layout"));
    }
}
