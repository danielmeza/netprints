using Microsoft.Extensions.Logging;

namespace NetPrints.Editor.Hosting.Automation;

/// <summary>Source-generated log messages for <see cref="AutomationAgent"/>.</summary>
internal static partial class Log
{
    /// <summary>Logs 1040: the automation agent's pipe accept loop, or one of its connections, failed.</summary>
    /// <param name="logger">Logger to write to.</param>
    /// <param name="exception">The exception that made it fail.</param>
    /// <param name="pipeName">Name of the agent's pipe.</param>
    /// <param name="what">What failed (e.g. "connection error", "stopped accepting connections").</param>
    [LoggerMessage(EventId = 1040, Level = LogLevel.Error, Message = "Automation agent '{PipeName}' {What}")]
    public static partial void AutomationAgentError(ILogger logger, Exception exception, string pipeName, string what);
}
