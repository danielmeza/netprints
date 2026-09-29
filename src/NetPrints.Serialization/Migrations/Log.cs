using Microsoft.Extensions.Logging;
using NetPrints.Serialization;

namespace NetPrints.Serialization.Migrations;

/// <summary>
/// Source-generated log messages for <c>NetPrints.Serialization.Migrations</c> (editor-services.md §6).
/// </summary>
internal static partial class Log
{
    /// <summary>Logs 3001: <see cref="DocumentMigrator.Upgrade"/> applied at least one migration to a document.</summary>
    /// <param name="logger">Logger to write to.</param>
    /// <param name="document">Id of the migrated document.</param>
    /// <param name="from">Schema version the document was read at.</param>
    /// <param name="to">Schema version the document was upgraded to.</param>
    [LoggerMessage(EventId = 3001, Level = LogLevel.Information, Message = "Migrated {Document} from schema {From} to {To}")]
    public static partial void DocumentMigrated(ILogger logger, DocumentId document, int from, int to);
}
