using NetPrints.Editor.Graph.Nodes;

namespace NetPrints.Editor.Graph;

/// <summary>
/// Selects nodes, optionally deselecting the previous selection.
/// </summary>
public sealed record NodeSelectionMessage(IReadOnlyList<NodeVM> Nodes, bool DeselectPrevious)
{
    public NodeSelectionMessage(NodeVM node) : this([node], true)
    {
    }

    public static NodeSelectionMessage DeselectAll => new([], true);
}
