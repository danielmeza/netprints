#nullable enable
using System;

namespace NetPrints.Serialization;

/// <summary>
/// A document could not be read or mapped: malformed JSON or XML, a schema version newer than
/// supported (see <see cref="DocumentVersionException"/>), a missing or duplicate id, or a reference
/// to something that does not exist.
/// </summary>
public class DocumentFormatException : Exception
{
    /// <summary>
    /// Creates a document format exception.
    /// </summary>
    /// <param name="message">Human-readable description of the problem.</param>
    /// <param name="document">Document the problem was found in, if known.</param>
    /// <param name="line">1-based line number of the problem, if known.</param>
    /// <param name="bytePosition">0-based byte offset of the problem, if known.</param>
    /// <param name="inner">Underlying exception, if any.</param>
    public DocumentFormatException(string message, DocumentId? document = null, long? line = null,
        long? bytePosition = null, Exception? inner = null)
        : base(message, inner)
    {
        Document = document;
        Line = line;
        BytePosition = bytePosition;
    }

    /// <summary>
    /// Document the problem was found in, or <see langword="null"/> if not known at the point the
    /// exception was raised.
    /// </summary>
    public DocumentId? Document { get; }

    /// <summary>
    /// 1-based line number of the problem, or <see langword="null"/> if not applicable/known.
    /// </summary>
    public long? Line { get; }

    /// <summary>
    /// 0-based byte offset of the problem, or <see langword="null"/> if not applicable/known.
    /// </summary>
    public long? BytePosition { get; }
}

/// <summary>
/// A document's <c>schemaVersion</c> is newer than this build of NetPrints supports.
/// </summary>
public sealed class DocumentVersionException : DocumentFormatException
{
    /// <summary>
    /// Creates a document version exception.
    /// </summary>
    /// <param name="found">The document's <c>schemaVersion</c>.</param>
    /// <param name="supported">The highest schema version this build supports.</param>
    /// <param name="document">Document the problem was found in, if known.</param>
    public DocumentVersionException(int found, int supported, DocumentId? document = null)
        : base($"Schema version {found} is newer than the supported version {supported}.", document)
    {
        Found = found;
        Supported = supported;
    }

    /// <summary>
    /// The document's <c>schemaVersion</c>.
    /// </summary>
    public int Found { get; }

    /// <summary>
    /// The highest schema version this build supports.
    /// </summary>
    public int Supported { get; }
}

/// <summary>
/// A requested document does not exist in the store.
/// </summary>
public sealed class DocumentNotFoundException : Exception
{
    /// <summary>
    /// Creates a document-not-found exception.
    /// </summary>
    /// <param name="document">The document that was not found.</param>
    public DocumentNotFoundException(DocumentId document)
        : base($"Document '{document}' was not found.")
    {
        Document = document;
    }

    /// <summary>
    /// The document that was not found.
    /// </summary>
    public DocumentId Document { get; }
}
