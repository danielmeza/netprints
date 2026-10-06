using Microsoft.Extensions.Logging;

namespace NetPrints.Editor.Lifecycle;

/// <summary>Source-generated log messages for <c>NetPrints.Editor.Lifecycle</c>.</summary>
internal static partial class Log
{
    /// <summary>Logs 1100: a manifest cannot be used.</summary>
    /// <param name="logger">Logger to write to.</param>
    /// <param name="path">The manifest's path.</param>
    [LoggerMessage(EventId = 1100, Level = LogLevel.Warning, Message = "The backup manifest {Path} has an unsupported format and is ignored")]
    public static partial void BackupManifestUnsupported(ILogger logger, string path);

    /// <summary>Logs 1101: a manifest cannot be read.</summary>
    /// <param name="logger">Logger to write to.</param>
    /// <param name="exception">What went wrong.</param>
    /// <param name="path">The manifest's path.</param>
    [LoggerMessage(EventId = 1101, Level = LogLevel.Warning, Message = "The backup manifest {Path} is unreadable and is ignored")]
    public static partial void BackupManifestUnreadable(ILogger logger, Exception exception, string path);

    /// <summary>Logs 1102: a backup could not be written or deleted.</summary>
    /// <param name="logger">Logger to write to.</param>
    /// <param name="exception">What went wrong.</param>
    [LoggerMessage(EventId = 1102, Level = LogLevel.Warning, Message = "A backup could not be written or deleted")]
    public static partial void BackupFailed(ILogger logger, Exception exception);

    /// <summary>Logs 1103: the startup clean-up of a backup folder failed.</summary>
    /// <param name="logger">Logger to write to.</param>
    /// <param name="exception">What went wrong.</param>
    /// <param name="folder">The backup folder.</param>
    [LoggerMessage(EventId = 1103, Level = LogLevel.Warning, Message = "Could not clean up the backups in {Folder}")]
    public static partial void BackupCleanUpFailed(ILogger logger, Exception exception, string folder);

    /// <summary>Logs 1104: a backup could not be restored.</summary>
    /// <param name="logger">Logger to write to.</param>
    /// <param name="exception">What went wrong.</param>
    /// <param name="path">The class path of the file.</param>
    [LoggerMessage(EventId = 1104, Level = LogLevel.Warning, Message = "The backup of {Path} could not be restored")]
    public static partial void RecoveryFailed(ILogger logger, Exception exception, string path);
}
