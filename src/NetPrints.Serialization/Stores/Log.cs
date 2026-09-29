using Microsoft.Extensions.Logging;

namespace NetPrints.Serialization.Stores;

/// <summary>
/// Source-generated log messages for <c>NetPrints.Serialization.Stores</c> (editor-services.md §6).
/// </summary>
internal static partial class Log
{
    /// <summary>Logs 3005: an external change to a document was detected.</summary>
    /// <param name="logger">Logger to write to.</param>
    /// <param name="kind">Kind of change.</param>
    /// <param name="document">Id of the changed document.</param>
    [LoggerMessage(EventId = 3005, Level = LogLevel.Debug, Message = "{Kind} {Document}")]
    public static partial void ExternalChange(ILogger logger, DocumentChangeKind kind, DocumentId document);
}
