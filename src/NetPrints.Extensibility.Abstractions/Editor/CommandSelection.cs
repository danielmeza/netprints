using System.Diagnostics.CodeAnalysis;
using NetPrints.Core;
using NetPrints.Graph;

namespace NetPrints.Extensibility.Editor;

/// <summary>What is selected when a command runs.</summary>
/// <param name="Nodes">The selected graph nodes of the active graph, as model nodes; empty when none. It is <see cref="IGraphEditor.SelectedNodes"/> at the moment the command runs.</param>
/// <param name="TreeItem">The selected project tree item, or null.</param>
[Experimental(ExperimentalApiIds.EditorContributions, UrlFormat = ExperimentalApiIds.UrlFormat)]
public sealed record CommandSelection(IReadOnlyList<Node> Nodes, object? TreeItem = null)
{
    /// <summary>Gets the empty selection.</summary>
    public static CommandSelection None { get; } = new([]);
}
