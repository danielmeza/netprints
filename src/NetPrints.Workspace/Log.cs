using Microsoft.Extensions.Logging;

namespace NetPrints.Workspace;

/// <summary>
/// Source-generated log messages for <c>NetPrints.Workspace</c> (project-system.md §4).
/// </summary>
internal static partial class Log
{
    /// <summary>Logs 4005: no MSBuild instance (Visual Studio or .NET SDK) could be found.</summary>
    /// <param name="logger">Logger to write to.</param>
    [LoggerMessage(EventId = 4005, Level = LogLevel.Warning, Message = "No MSBuild instance found; MSBuild-dependent project system features are unavailable")]
    public static partial void NoMsBuildInstanceFound(ILogger logger);
}
