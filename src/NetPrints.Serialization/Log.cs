using Microsoft.Extensions.Logging;

namespace NetPrints.Serialization;

/// <summary>
/// Source-generated log messages for the root <c>NetPrints.Serialization</c> namespace
/// (editor-services.md §6).
/// </summary>
internal static partial class Log
{
    /// <summary>Logs 3007: <see cref="ProjectPersistence.LoadAsync"/> skipped a class graph it could
    /// not read.</summary>
    /// <param name="logger">Logger to write to.</param>
    /// <param name="document">Id of the document that could not be read.</param>
    /// <param name="reason">The read failure's message.</param>
    [LoggerMessage(EventId = 3007, Level = LogLevel.Warning, Message = "Class graph {Document} could not be loaded: {Reason}")]
    public static partial void ClassLoadFailed(ILogger logger, DocumentId document, string reason);
}
