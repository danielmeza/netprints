using NetPrints.Core;

namespace NetPrints.Extensibility.Nodes;

/// <summary>
/// Maps an open graph to its <see cref="GraphKinds"/> value, for filtering node suggestions.
/// </summary>
public static class NodeGraphKinds
{
    /// <summary>
    /// Returns the kind of <paramref name="graph"/>.
    /// </summary>
    /// <param name="graph">The graph.</param>
    /// <returns>The matching kind, or <see cref="GraphKinds.None"/> for a graph type this version does not know.</returns>
    public static GraphKinds Of(NodeGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        return graph switch
        {
            MethodGraph => GraphKinds.Method,
            ConstructorGraph => GraphKinds.Constructor,
            ClassGraph => GraphKinds.Class,
            TypeGraph => GraphKinds.Type,
            EventGraph => GraphKinds.Event,
            _ => GraphKinds.None,
        };
    }
}
