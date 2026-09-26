using Microsoft.Extensions.Logging;

namespace NetPrints.Workspace;

/// <summary>
/// Source-generated log messages for <c>NetPrints.Workspace</c> (project-system.md §4).
/// </summary>
internal static partial class Log
{
    /// <summary>Logs 4001: <c>MsBuildProjectSystem.LoadAsync</c> is restoring a stale project out of process.</summary>
    /// <param name="logger">Logger to write to.</param>
    /// <param name="projectFilePath">Path of the project being restored.</param>
    [LoggerMessage(EventId = 4001, Level = LogLevel.Debug, Message = "Restoring {ProjectFilePath}")]
    public static partial void Restoring(ILogger logger, string projectFilePath);

    /// <summary>Logs 4002: an out-of-process restore failed; <c>LoadAsync</c> continues with a <see cref="NetPrints.Projects.ProjectMessage.RestoreFailed"/> message.</summary>
    /// <param name="logger">Logger to write to.</param>
    /// <param name="projectFilePath">Path of the project that failed to restore.</param>
    /// <param name="exitCode">The restore process's exit code.</param>
    [LoggerMessage(EventId = 4002, Level = LogLevel.Warning, Message = "Restore failed for {ProjectFilePath} (exit code {ExitCode})")]
    public static partial void RestoreFailed(ILogger logger, string projectFilePath, int exitCode);

    /// <summary>Logs 4003: <c>MsBuildProjectSystem.LoadAsync</c> finished loading a project.</summary>
    /// <param name="logger">Logger to write to.</param>
    /// <param name="projectFilePath">Path of the project that was loaded.</param>
    [LoggerMessage(EventId = 4003, Level = LogLevel.Debug, Message = "Loaded {ProjectFilePath}")]
    public static partial void Loaded(ILogger logger, string projectFilePath);

    /// <summary>Logs 4004: <c>MsBuildProjectSystem.BuildAsync</c> finished building a project.</summary>
    /// <param name="logger">Logger to write to.</param>
    /// <param name="projectFilePath">Path of the project that was built.</param>
    /// <param name="success">Whether the build succeeded.</param>
    [LoggerMessage(EventId = 4004, Level = LogLevel.Debug, Message = "Built {ProjectFilePath}: succeeded={Success}")]
    public static partial void Built(ILogger logger, string projectFilePath, bool success);

    /// <summary>Logs 4005: no MSBuild instance (Visual Studio or .NET SDK) could be found.</summary>
    /// <param name="logger">Logger to write to.</param>
    [LoggerMessage(EventId = 4005, Level = LogLevel.Warning, Message = "No MSBuild instance found; MSBuild-dependent project system features are unavailable")]
    public static partial void NoMsBuildInstanceFound(ILogger logger);
}
