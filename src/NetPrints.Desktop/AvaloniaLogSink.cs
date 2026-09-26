using System;
using Avalonia.Logging;
using Microsoft.Extensions.Logging;

namespace NetPrints.Desktop;

/// <summary>
/// Forwards Avalonia's internal logging (set as <c>Avalonia.Logging.Logger.Sink</c>) to
/// <see cref="ILogger"/> categories named <c>Avalonia.&lt;Area&gt;</c> (editor-services.md §6,
/// FR-042), replacing <c>AppBuilder.LogToTrace()</c>.
/// </summary>
public sealed class AvaloniaLogSink : ILogSink
{
    private readonly ILoggerFactory loggerFactory;

    /// <summary>
    /// Creates a sink that resolves its per-area loggers from <paramref name="loggerFactory"/>.
    /// </summary>
    /// <param name="loggerFactory">Used to create one logger per distinct Avalonia log area.</param>
    /// <exception cref="ArgumentNullException"><paramref name="loggerFactory"/> is <see langword="null"/>.</exception>
    public AvaloniaLogSink(ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        this.loggerFactory = loggerFactory;
    }

    private ILogger LoggerFor(string area) => loggerFactory.CreateLogger($"Avalonia.{area}");

    private static LogLevel ToLogLevel(LogEventLevel level) => level switch
    {
        LogEventLevel.Verbose => LogLevel.Trace,
        LogEventLevel.Debug => LogLevel.Debug,
        LogEventLevel.Information => LogLevel.Information,
        LogEventLevel.Warning => LogLevel.Warning,
        LogEventLevel.Error => LogLevel.Error,
        LogEventLevel.Fatal => LogLevel.Critical,
        _ => LogLevel.Information,
    };

    private static string FormatMessage(string messageTemplate, object?[] propertyValues) =>
        propertyValues.Length == 0 ? messageTemplate : $"{messageTemplate} {string.Join(' ', propertyValues)}";

    /// <inheritdoc/>
    public bool IsEnabled(LogEventLevel level, string area) => LoggerFor(area).IsEnabled(ToLogLevel(level));

    /// <inheritdoc/>
    public void Log(LogEventLevel level, string area, object? source, string messageTemplate) =>
        NetPrints.Desktop.Log.AvaloniaForwarded(LoggerFor(area), ToLogLevel(level), source, messageTemplate);

    /// <inheritdoc/>
    public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues) =>
        NetPrints.Desktop.Log.AvaloniaForwarded(LoggerFor(area), ToLogLevel(level), source, FormatMessage(messageTemplate, propertyValues));
}
