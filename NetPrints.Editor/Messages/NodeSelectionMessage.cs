using NetPrints.Editor.ViewModels;

namespace NetPrints.Editor.Messages;

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
