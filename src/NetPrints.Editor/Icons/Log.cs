using Microsoft.Extensions.Logging;

namespace NetPrints.Editor.Icons;

/// <summary>Source-generated log messages for <c>NetPrints.Editor.Icons</c>.</summary>
internal static partial class Log
{
    /// <summary>Logs 1080: an icon id is not in the registry and the fallback glyph is drawn.</summary>
    /// <param name="logger">Logger to write to.</param>
    /// <param name="id">The unknown icon id.</param>
    [LoggerMessage(EventId = 1080, Level = LogLevel.Warning, Message = "Unknown icon id {Id}; drawing the fallback glyph")]
    public static partial void UnknownIcon(ILogger logger, string id);
}
