using Microsoft.Extensions.Logging;

namespace NetPrints.Desktop;

/// <summary>
/// Source-generated log messages for <c>NetPrints.Desktop</c>.
/// </summary>
internal static partial class Log
{
    /// <summary>Logs a message forwarded from Avalonia's internal logging (<see cref="AvaloniaLogSink"/>).</summary>
    /// <param name="logger">Logger to write to (one per Avalonia log area).</param>
    /// <param name="level">Avalonia's own level, mapped to <see cref="LogLevel"/>.</param>
    /// <param name="source">The Avalonia object the message is about, if any.</param>
    /// <param name="message">The message text (Avalonia's template, with any property values already formatted in).</param>
    [LoggerMessage(Message = "{Source}: {Message}")]
    public static partial void AvaloniaForwarded(ILogger logger, LogLevel level, object? source, string message);
}
