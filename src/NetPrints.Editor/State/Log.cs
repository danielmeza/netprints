using Microsoft.Extensions.Logging;

namespace NetPrints.Editor.State;

/// <summary>Source-generated log messages for <c>NetPrints.Editor.State</c>.</summary>
internal static partial class Log
{
    /// <summary>Logs 1200: a state file cannot be used and the defaults apply.</summary>
    /// <param name="logger">Logger to write to.</param>
    /// <param name="path">The state file's path.</param>
    [LoggerMessage(EventId = 1200, Level = LogLevel.Warning, Message = "The state file {Path} has an unsupported format or a newer version and is ignored")]
    public static partial void StateFileUnsupported(ILogger logger, string path);

    /// <summary>Logs 1201: a state file cannot be read and the defaults apply.</summary>
    /// <param name="logger">Logger to write to.</param>
    /// <param name="exception">What went wrong.</param>
    /// <param name="path">The state file's path.</param>
    [LoggerMessage(EventId = 1201, Level = LogLevel.Warning, Message = "The state file {Path} is unreadable and is ignored")]
    public static partial void StateFileUnreadable(ILogger logger, Exception exception, string path);

    /// <summary>Logs 1202: a state file cannot be written.</summary>
    /// <param name="logger">Logger to write to.</param>
    /// <param name="exception">What went wrong.</param>
    /// <param name="path">The state file's path.</param>
    [LoggerMessage(EventId = 1202, Level = LogLevel.Warning, Message = "The state file {Path} could not be written")]
    public static partial void StateFileWriteFailed(ILogger logger, Exception exception, string path);
}
