using Microsoft.Extensions.Logging;

namespace NetPrints.Editor.Main;

/// <summary>
/// Source-generated log messages for <c>NetPrints.Editor.Main</c> (editor-services.md §6).
/// </summary>
internal static partial class Log
{
    /// <summary>Logs 1041: <see cref="MainEditorViewModel.LoadProjectAsync"/> restored the previous project's
    /// extensions after a failed load, and that restore itself failed. The original load failure is
    /// still shown to the user.</summary>
    /// <param name="logger">Logger to write to.</param>
    /// <param name="exception">The exception the rollback threw.</param>
    [LoggerMessage(EventId = 1041, Level = LogLevel.Error, Message = "Restoring the previous project's extensions after a failed load also failed")]
    public static partial void ExtensionRollbackFailed(ILogger logger, Exception exception);
}
