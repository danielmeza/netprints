using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia.Logging;
using Microsoft.Extensions.Logging;

namespace NetPrints.Desktop;

/// <summary>
/// Forwards Avalonia's internal logging (set as <c>Avalonia.Logging.Logger.Sink</c>) to
/// <see cref="ILogger"/> categories named <c>Avalonia.&lt;Area&gt;</c> (editor-services.md §6,
/// FR-042), replacing <c>AppBuilder.LogToTrace()</c>.
/// </summary>
public sealed partial class AvaloniaLogSink : ILogSink
{
    /// <summary>
    /// Areas Avalonia logs at <see cref="LogEventLevel.Information"/> on every layout/render pass
    /// (batch D1: opening a small graph logged several of these per click, at Information, with no
    /// user-visible feedback to explain the delay). Floored to <see cref="LogLevel.Warning"/> unless
    /// <c>NETPRINTS_LOG_LEVEL</c> explicitly asks for something more verbose.
    /// </summary>
    private static readonly HashSet<string> ChattyAreas = new(StringComparer.Ordinal) { "Layout", "Visual" };

    private const string ImeArea = "IME";
    private const string IbusSourceType = "IBusX11TextInputMethod";
    private const string DBusReplyException = "DBusErrorReplyException";
    private static readonly string[] IbusTeardownPrefixes = ["Error while destroying the context", "Error:"];
    private static readonly string[] IbusTeardownFragments = ["UnknownMethod", "/org/freedesktop/IBus/InputContext_"];

    private static readonly Regex PlaceholderPattern = MessageTemplatePlaceholder();

    private readonly ILoggerFactory loggerFactory;
    private readonly LogLevel? explicitMinimumLevel;
    private readonly Dictionary<string, ILogger> loggerCache = new(StringComparer.Ordinal);

    /// <summary>
    /// Creates a sink that resolves its per-area loggers from <paramref name="loggerFactory"/>.
    /// </summary>
    /// <param name="loggerFactory">Used to create one logger per distinct Avalonia log area.</param>
    /// <param name="explicitMinimumLevel">The level <c>NETPRINTS_LOG_LEVEL</c> named, or
    /// <see langword="null"/> if it was not set: when null, <see cref="ChattyAreas"/> are floored to
    /// <see cref="LogLevel.Warning"/> regardless of <paramref name="loggerFactory"/>'s own minimum.</param>
    /// <exception cref="ArgumentNullException"><paramref name="loggerFactory"/> is <see langword="null"/>.</exception>
    public AvaloniaLogSink(ILoggerFactory loggerFactory, LogLevel? explicitMinimumLevel = null)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        this.loggerFactory = loggerFactory;
        this.explicitMinimumLevel = explicitMinimumLevel;
    }

    private ILogger LoggerFor(string area)
    {
        if (!loggerCache.TryGetValue(area, out var logger))
        {
            logger = loggerFactory.CreateLogger($"Avalonia.{area}");
            loggerCache[area] = logger;
        }
        return logger;
    }

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

    /// <summary>Substitutes each <c>{Placeholder}</c> in <paramref name="messageTemplate"/> with the
    /// matching entry of <paramref name="propertyValues"/>, in order, instead of leaving the
    /// placeholders raw and appending the values after the template.</summary>
    private static string FormatMessage(string messageTemplate, object?[] propertyValues)
    {
        if (propertyValues.Length == 0)
        {
            return messageTemplate;
        }

        int index = 0;
        return PlaceholderPattern.Replace(messageTemplate, _ =>
            index < propertyValues.Length ? FormatValue(propertyValues[index++]) : "");
    }

    private static string FormatValue(object? value) => value switch
    {
        null => "null",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "",
    };

    /// <summary>Downgrades the two IBus context-teardown errors Avalonia logs (upstream #15551) to Debug; the daemon localises its text, so it matches the exception type and the method or path fragment.</summary>
    private static LogLevel LevelFor(LogLevel mapped, string area, object? source, string message) =>
        mapped == LogLevel.Error
        && area == ImeArea
        && source?.GetType().Name == IbusSourceType
        && IbusTeardownPrefixes.Any(prefix => message.StartsWith(prefix, StringComparison.Ordinal))
        && message.Contains(DBusReplyException, StringComparison.Ordinal)
        && IbusTeardownFragments.Any(fragment => message.Contains(fragment, StringComparison.Ordinal))
            ? LogLevel.Debug
            : mapped;

    /// <inheritdoc/>
    public bool IsEnabled(LogEventLevel level, string area)
    {
        LogLevel mapped = ToLogLevel(level);
        if (explicitMinimumLevel is null && ChattyAreas.Contains(area) && mapped < LogLevel.Warning)
        {
            return false;
        }

        return LoggerFor(area).IsEnabled(mapped);
    }

    /// <inheritdoc/>
    public void Log(LogEventLevel level, string area, object? source, string messageTemplate) =>
        NetPrints.Desktop.Log.AvaloniaForwarded(LoggerFor(area), LevelFor(ToLogLevel(level), area, source, messageTemplate), source, messageTemplate);

    /// <inheritdoc/>
    public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues)
    {
        string message = FormatMessage(messageTemplate, propertyValues);
        NetPrints.Desktop.Log.AvaloniaForwarded(LoggerFor(area), LevelFor(ToLogLevel(level), area, source, message), source, message);
    }

    [GeneratedRegex(@"\{\$?[^{}]+\}")]
    private static partial Regex MessageTemplatePlaceholder();
}
