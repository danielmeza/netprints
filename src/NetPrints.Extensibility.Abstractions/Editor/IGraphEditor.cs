using System.Diagnostics.CodeAnalysis;
using NetPrints.Core;
using NetPrints.Graph;

namespace NetPrints.Extensibility.Editor;

/// <summary>A graph open in the editor: its model, its selection, its undo history, and the one way to add a node.</summary>
[Experimental(ExperimentalApiIds.EditorContributions, UrlFormat = ExperimentalApiIds.UrlFormat)]
public interface IGraphEditor
{
    /// <summary>Gets the graph.</summary>
    NodeGraph Graph { get; }

    /// <summary>Gets the selected nodes.</summary>
    IReadOnlyList<Node> SelectedNodes { get; }

    /// <summary>Gets the undo history of the graph's class.</summary>
    IEditorUndo Undo { get; }

    /// <summary>Adds a node as one undo step, clamped to the canvas and connected to <paramref name="connectTo"/>.</summary>
    /// <param name="create">Creates the node in the graph.</param>
    /// <param name="position">Where the node is created (graph coordinates).</param>
    /// <param name="connectTo">The pin to connect the node to, or <see langword="null"/>.</param>
    /// <returns>The new node.</returns>
    Node Place(Func<NodeGraph, Node> create, GraphPoint position, NodePin? connectTo);
}
