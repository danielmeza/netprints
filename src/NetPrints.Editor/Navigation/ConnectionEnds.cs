using NetPrints.Editor.Graph;

namespace NetPrints.Editor.Navigation;

/// <summary>One end of a connection.</summary>
public enum ConnectionEnd
{
    /// <summary>The output-side pin.</summary>
    Source,

    /// <summary>The input-side pin.</summary>
    Target,
}

/// <summary>Picks the end of a connection to go to (FR-062).</summary>
public static class ConnectionEnds
{
    /// <summary>Gets the end that is farther from a click on the connection.</summary>
    /// <param name="click">The click, in graph units.</param>
    /// <param name="source">The anchor of the output-side pin.</param>
    /// <param name="target">The anchor of the input-side pin.</param>
    /// <returns>The farther end; the target when both are equally far.</returns>
    public static ConnectionEnd Farther(GraphPoint click, GraphPoint source, GraphPoint target) =>
        DistanceSquared(click, source) > DistanceSquared(click, target) ? ConnectionEnd.Source : ConnectionEnd.Target;

    private static double DistanceSquared(GraphPoint a, GraphPoint b)
    {
        GraphPoint difference = a - b;
        return (difference.X * difference.X) + (difference.Y * difference.Y);
    }
}
