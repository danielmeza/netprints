using NetPrints.Core;
using NetPrints.Graph;

namespace NetPrints.Editor.Graph;

/// <summary>Test helper: creates a node by its type and constructor arguments, which the editor itself does with factories.</summary>
public static class NodeGraphTestExtensions
{
    /// <summary>Creates a <typeparamref name="T"/> at <paramref name="position"/> through the graph's undoable factory path.</summary>
    /// <typeparam name="T">The node type.</typeparam>
    /// <param name="graph">The graph view model.</param>
    /// <param name="position">Where the node is created.</param>
    /// <param name="suggestionPin">Pin to connect to, or <see langword="null"/>.</param>
    /// <param name="arguments">Constructor arguments after the graph.</param>
    /// <returns>The new node.</returns>
    public static Node AddNode<T>(this NodeGraphViewModel graph, GraphPoint position, NodePin? suggestionPin = null, params object[] arguments)
        where T : Node
    {
        ArgumentNullException.ThrowIfNull(graph);
        return graph.AddNode(position, suggestionPin, g => (T)(Activator.CreateInstance(typeof(T), [g, .. arguments])
            ?? throw new InvalidOperationException($"Could not create {typeof(T)}.")));
    }
}
