namespace NetPrints.Editor.Shell;

/// <summary>What is selected when a command runs.</summary>
/// <param name="Nodes">The selected graph nodes (view models); empty when none.</param>
/// <param name="TreeItem">The selected project tree item, or null.</param>
public sealed record CommandSelection(IReadOnlyList<object> Nodes, object? TreeItem = null)
{
    /// <summary>Gets the empty selection.</summary>
    public static CommandSelection None { get; } = new([]);
}
