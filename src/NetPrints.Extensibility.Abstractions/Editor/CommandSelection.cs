namespace NetPrints.Extensibility.Editor;

/// <summary>What is selected when a command runs.</summary>
/// <param name="Nodes">The selected graph nodes as the host represents them (view models in the editor); empty when none. <see cref="IGraphEditor.SelectedNodes"/> has the model nodes.</param>
/// <param name="TreeItem">The selected project tree item, or null.</param>
public sealed record CommandSelection(IReadOnlyList<object> Nodes, object? TreeItem = null)
{
    /// <summary>Gets the empty selection.</summary>
    public static CommandSelection None { get; } = new([]);
}
