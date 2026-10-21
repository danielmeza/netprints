using NetPrints.Editor.Graph;
using NetPrints.Extensibility.Nodes;
using NetPrints.Graph;

namespace NetPrints.Editor.NodeSearch;

/// <summary>Builds the public search context from the editor's graph view model.</summary>
internal static class NodeSearchContexts
{
    /// <summary>Creates the context of a search.</summary>
    /// <param name="graph">The graph view model the search is opened for.</param>
    /// <param name="pin">The pin the search was opened from, or <see langword="null"/> for the canvas.</param>
    /// <returns>The context.</returns>
    public static NodeSearchContext For(NodeGraphViewModel graph, NodePin? pin)
    {
        ArgumentNullException.ThrowIfNull(graph);
        return new NodeSearchContext(graph.Graph, pin, graph.Context.Reflection.Provider, graph.Context.Extensions.Current.NodeKinds);
    }
}
