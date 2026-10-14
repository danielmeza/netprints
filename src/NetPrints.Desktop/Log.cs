using System;
using Microsoft.Extensions.Logging;

namespace NetPrints.Desktop;

/// <summary>
/// Source-generated log messages for <c>NetPrints.Desktop</c>.
/// </summary>
internal static partial class Log
{
    /// <summary>Logs an exception that reached <see cref="AppDomain.UnhandledException"/> (<see cref="Logging.CrashHandler"/>).</summary>
    /// <param name="logger">Logger to write to.</param>
    /// <param name="exception">The exception, or null when the thrown object was not an exception.</param>
    /// <param name="isTerminating">Whether the runtime terminates the process.</param>
    /// <param name="reason">The exception's message, or the thrown object's text.</param>
    [LoggerMessage(EventId = 1300, Level = LogLevel.Critical, Message = "Unhandled exception (terminating: {IsTerminating}): {Reason}")]
    public static partial void UnhandledException(ILogger logger, Exception? exception, bool isTerminating, string reason);

    /// <summary>Logs a message forwarded from Avalonia's internal logging (<see cref="AvaloniaLogSink"/>).</summary>
    /// <param name="logger">Logger to write to (one per Avalonia log area).</param>
    /// <param name="level">Avalonia's own level, mapped to <see cref="LogLevel"/>.</param>
    /// <param name="source">The Avalonia object the message is about, if any.</param>
    /// <param name="message">The message text (Avalonia's template, with any property values already formatted in).</param>
    [LoggerMessage(Message = "{Source}: {Message}")]
    public static partial void AvaloniaForwarded(ILogger logger, LogLevel level, object? source, string message);
}
