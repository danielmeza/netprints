#nullable enable
using System.Collections.Generic;
using NetPrints.Core;
using NetPrints.Serialization.Documents;

namespace NetPrints.Serialization.Mapping;

/// <summary>
/// Maps a <see cref="ClassGraph"/> to and from its <see cref="ClassDocument"/> form (document-format.md
/// §2.6). Implemented by <see cref="DocumentMapper"/>; consumed by <c>JsonDocumentFormat</c>,
/// <c>LegacyXmlDocumentFormat</c>, <c>ProjectPersistence</c> and <c>GraphCodeGenerator</c>.
/// </summary>
public interface IDocumentMapper
{
    /// <summary>
    /// Converts <paramref name="cls"/> to its document form. Pure: does not mutate <paramref name="cls"/>.
    /// </summary>
    /// <param name="cls">Class to convert.</param>
    /// <returns>The class's document form.</returns>
    ClassDocument ToDocument(ClassGraph cls);

    /// <summary>
    /// Builds a <see cref="ClassGraph"/> from <paramref name="document"/>.
    /// </summary>
    /// <param name="document">Document to convert.</param>
    /// <param name="project">Project the built class belongs to.</param>
    /// <param name="issues">Collection non-fatal problems (dropped connections or pin states, ignored
    /// layout entries, preserved unknown nodes) are added to.</param>
    /// <param name="id">Id of the document being converted, used in exception messages and issues.</param>
    /// <returns>The built class, with <see cref="ClassGraph.IsDirty"/> <see langword="false"/>.</returns>
    ClassGraph FromDocument(ClassDocument document, Project project, ICollection<DocumentIssue> issues, DocumentId id);
}
