using Microsoft.Extensions.Logging;

namespace NetPrints.Editor.Diagnostics;

/// <summary>
/// Source-generated log messages for <c>NetPrints.Editor.Diagnostics</c> (editor-services.md §6).
/// </summary>
internal static partial class Log
{
    /// <summary>
    /// Logs 1060: <see cref="CodeAnalysisHost"/>'s debounced translation or analysis failed; the
    /// previous snapshot, if any, is kept. editor-services.md §6 names this event 1030
    /// (<c>CodeAnalysisFailed</c>), but that id was already taken by <c>Hosting.Log.TaskFaulted</c>
    /// (Sub-phase I, batch I2 decision): 1060 continues the per-feature-folder block numbering after
    /// the last reserved block (1050, <c>GridShaderUnavailable</c>).
    /// </summary>
    /// <param name="logger">Logger to write to.</param>
    /// <param name="exception">The exception that made the analysis fail.</param>
    /// <param name="projectName">Name of the project being analyzed.</param>
    [LoggerMessage(EventId = 1060, Level = LogLevel.Warning, Message = "Code analysis failed for {ProjectName}")]
    public static partial void CodeAnalysisFailed(ILogger logger, Exception exception, string projectName);
}
