using Microsoft.Extensions.Logging;

namespace NetPrints.Editor.Shell.Docking;

/// <summary>Source-generated log messages for <c>NetPrints.Editor.Shell.Docking</c>.</summary>
internal static partial class Log
{
    /// <summary>Logs 1210: the saved layout cannot be used and the default layout applies.</summary>
    /// <param name="logger">Logger to write to.</param>
    /// <param name="exception">What went wrong, or null when the layout was well-formed but unusable.</param>
    /// <param name="reason">Why.</param>
    [LoggerMessage(EventId = 1210, Level = LogLevel.Warning, Message = "The saved layout cannot be restored ({Reason}); the default layout is used")]
    public static partial void LayoutUnusable(ILogger logger, Exception? exception, string reason);
}
