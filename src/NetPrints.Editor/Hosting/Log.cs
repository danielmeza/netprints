using Microsoft.Extensions.Logging;

namespace NetPrints.Editor.Hosting;

/// <summary>
/// Source-generated log messages for <c>NetPrints.Editor.Hosting</c> (editor-services.md §6).
/// </summary>
internal static partial class Log
{
    /// <summary>Logs 1001: an exception reached the UI thread unhandled.</summary>
    /// <param name="logger">Logger to write to.</param>
    /// <param name="exception">The exception that escaped to the UI thread.</param>
    [LoggerMessage(EventId = 1001, Level = LogLevel.Error, Message = "Unhandled exception on the UI thread")]
    public static partial void UnhandledUiException(ILogger logger, Exception exception);

    /// <summary>Logs 1002: a task's exception was never observed.</summary>
    /// <param name="logger">Logger to write to.</param>
    /// <param name="exception">The unobserved exception.</param>
    [LoggerMessage(EventId = 1002, Level = LogLevel.Error, Message = "Unobserved task exception")]
    public static partial void UnobservedTaskException(ILogger logger, Exception exception);
}
