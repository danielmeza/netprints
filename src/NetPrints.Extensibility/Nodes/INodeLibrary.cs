namespace NetPrints.Extensibility.Nodes;

/// <summary>
/// A named set of node kinds (extension-points.md §2).
/// </summary>
public interface INodeLibrary
{
    /// <summary>
    /// The extension id, or <c>"&lt;extension id&gt;/&lt;library&gt;"</c>.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// The node kinds of this library.
    /// </summary>
    IReadOnlyList<NodeKindDescriptor> NodeKinds { get; }
}
