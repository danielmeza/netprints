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

    /// <summary>Logs 1204: the one-time move of a state folder failed and the folder stays where it was.</summary>
    /// <param name="logger">Logger to write to.</param>
    /// <param name="exception">What went wrong.</param>
    /// <param name="source">The former folder.</param>
    /// <param name="destination">The new folder.</param>
    [LoggerMessage(EventId = 1204, Level = LogLevel.Warning, Message = "The state folder {Source} could not be moved to {Destination}; the editor starts with defaults")]
    public static partial void StateMigrationFailed(ILogger logger, Exception exception, string source, string destination);

    /// <summary>Logs 1205: the emptied former root could not be removed.</summary>
    /// <param name="logger">Logger to write to.</param>
    /// <param name="exception">What went wrong.</param>
    /// <param name="path">The former root.</param>
    [LoggerMessage(EventId = 1205, Level = LogLevel.Warning, Message = "The former state root {Path} could not be removed")]
    public static partial void LegacyRootNotRemoved(ILogger logger, Exception exception, string path);

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

    /// <summary>Logs 1203: a save is skipped because the file was written by a newer editor.</summary>
    /// <param name="logger">Logger to write to.</param>
    /// <param name="path">The state file's path.</param>
    [LoggerMessage(EventId = 1203, Level = LogLevel.Warning, Message = "The state file {Path} is from a newer editor and is not rewritten")]
    public static partial void StateFileSaveSkipped(ILogger logger, string path);
}
