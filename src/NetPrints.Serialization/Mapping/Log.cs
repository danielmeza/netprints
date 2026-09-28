using Microsoft.Extensions.Logging;
using NetPrints.Serialization;

namespace NetPrints.Serialization.Mapping;

/// <summary>
/// Source-generated log messages for <c>NetPrints.Serialization.Mapping</c> (editor-services.md §6).
/// </summary>
internal static partial class Log
{
    /// <summary>Logs 3002: <see cref="DocumentMapper.FromDocument"/> preserved a node of a kind no converter recognizes.</summary>
    /// <param name="logger">Logger to write to.</param>
    /// <param name="nodeId">Id of the preserved node.</param>
    /// <param name="kind">The node's unrecognized <c>$kind</c>.</param>
    /// <param name="document">Id of the document the node was read from.</param>
    [LoggerMessage(EventId = 3002, Level = LogLevel.Warning, Message = "Node {NodeId} of unknown kind {Kind} in {Document} is preserved")]
    public static partial void UnknownNodeKindPreserved(ILogger logger, string nodeId, string kind, DocumentId document);

    /// <summary>Logs 3003: <see cref="DocumentMapper.FromDocument"/> dropped a connection it could not restore.</summary>
    /// <param name="logger">Logger to write to.</param>
    /// <param name="from">The connection's source endpoint.</param>
    /// <param name="to">The connection's target endpoint.</param>
    /// <param name="document">Id of the document the connection was read from.</param>
    /// <param name="reason">Why the connection was dropped.</param>
    [LoggerMessage(EventId = 3003, Level = LogLevel.Warning, Message = "Connection {From} -> {To} in {Document} dropped: {Reason}")]
    public static partial void ConnectionDropped(ILogger logger, string from, string to, DocumentId document, string reason);
}
