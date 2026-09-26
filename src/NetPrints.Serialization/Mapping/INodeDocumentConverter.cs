#nullable enable
using System;
using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Serialization.Documents;

namespace NetPrints.Serialization.Mapping;

/// <summary>
/// Converts one node kind between its model type and its document type (document-format.md §1.5,
/// §2.6). Built-in converters are registered by <see cref="NodeDocumentConverterRegistry.BuiltIn"/>;
/// extensions contribute their own (extension-points.md §2).
/// </summary>
public interface INodeDocumentConverter
{
    /// <summary>
    /// The <c>$kind</c> value this converter handles: one of the built-in names of document-format.md
    /// §1.5 (no <c>/</c>), or <c>"&lt;extension id&gt;/&lt;name&gt;"</c> for an extension kind.
    /// </summary>
    string Kind { get; }

    /// <summary>
    /// The exact runtime <see cref="NetPrints.Graph.Node"/> type this converter creates and reads.
    /// </summary>
    Type NodeType { get; }

    /// <summary>
    /// The concrete <see cref="NodeDocument"/> subtype this converter writes and reads.
    /// </summary>
    Type DocumentType { get; }

    /// <summary>
    /// Converts <paramref name="node"/> to its document form. Pure: does not mutate <paramref name="node"/>.
    /// </summary>
    /// <param name="node">Node to convert; its runtime type is exactly <see cref="NodeType"/>.</param>
    /// <param name="context">Mapping context for the node's class.</param>
    /// <returns>The node's document form.</returns>
    NodeDocument ToDocument(Node node, NodeMappingContext context);

    /// <summary>
    /// Creates the node described by <paramref name="document"/> in <paramref name="graph"/>. The
    /// created node's id, name and pin states (renames, unconnected values) are applied by the caller
    /// (document-format.md §2.6); this method only needs to give the node the right kind-specific pins.
    /// </summary>
    /// <param name="document">Document to create the node from; its runtime type is exactly
    /// <see cref="DocumentType"/>.</param>
    /// <param name="graph">Graph the node is created in.</param>
    /// <param name="context">Mapping context for the node's class.</param>
    /// <returns>The created node.</returns>
    Node CreateNode(NodeDocument document, NodeGraph graph, NodeMappingContext context);
}
