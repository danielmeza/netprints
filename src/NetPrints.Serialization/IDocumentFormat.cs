#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NetPrints.Serialization.Documents;

namespace NetPrints.Serialization;

/// <summary>
/// Reads and writes class graph documents in one on-disk representation: the canonical JSON form
/// (<c>.netpc.json</c>) or the legacy DataContract XML (<c>.netpc</c>, read-only). Implementations are
/// immutable and safe to use concurrently (document-format.md §2.2).
/// </summary>
public interface IDocumentFormat
{
    /// <summary>Stable identifier (<c>"json"</c> or <c>"legacy-xml"</c>).</summary>
    string Id { get; }

    /// <summary>
    /// File extensions this format claims (<c>".netpc.json"</c> for the JSON format,
    /// <c>".netpc"</c> for the legacy XML format), longest first so a registry matching by suffix can
    /// prefer the more specific match.
    /// </summary>
    IReadOnlyList<string> ClassExtensions { get; }

    /// <summary>Whether <see cref="WriteClassAsync"/> is supported.</summary>
    bool CanWrite { get; }

    /// <summary>
    /// Reads a class document from <paramref name="input"/>. Older schema versions are migrated to
    /// the version this build supports first.
    /// </summary>
    /// <param name="input">Stream to read from. Not disposed by this method.</param>
    /// <param name="id">Id of the document being read, used in exceptions and issues.</param>
    /// <param name="cancellationToken">Token to cancel the read.</param>
    /// <returns>The document read from <paramref name="input"/>.</returns>
    /// <exception cref="DocumentFormatException">The content is malformed.</exception>
    /// <exception cref="DocumentVersionException">The document's schema version is newer than this
    /// build supports.</exception>
    ValueTask<ClassDocument> ReadClassAsync(Stream input, DocumentId id, CancellationToken cancellationToken);

    /// <summary>
    /// Writes <paramref name="document"/> to <paramref name="output"/> in this format's canonical
    /// form: the same document always writes the same bytes.
    /// </summary>
    /// <param name="document">Document to write.</param>
    /// <param name="output">Stream to write to. Not disposed by this method.</param>
    /// <param name="cancellationToken">Token to cancel the write.</param>
    /// <exception cref="NotSupportedException"><see cref="CanWrite"/> is <see langword="false"/>.</exception>
    ValueTask WriteClassAsync(ClassDocument document, Stream output, CancellationToken cancellationToken);
}
