using NetPrints.Editor.Graph;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Navigation;

/// <summary>One place in the navigation history: a graph and how the canvas showed it.</summary>
/// <param name="Document">The graph document.</param>
/// <param name="Location">The canvas location in graph units.</param>
/// <param name="Zoom">The canvas zoom, 1 being full size.</param>
/// <param name="SelectedNodeIds">The ids of the selected nodes, in selection order.</param>
public sealed record NavigationEntry(DocumentId Document, GraphPoint Location, double Zoom, IReadOnlyList<string> SelectedNodeIds)
{
    /// <summary>Gets whether <paramref name="other"/> shows the same graph, viewport and selection.</summary>
    /// <param name="other">The entry to compare with, or null.</param>
    /// <returns><see langword="true"/> when both entries restore the same view.</returns>
    public bool SameView(NavigationEntry? other) =>
        other is not null
        && Document == other.Document
        && Location == other.Location
        && Zoom.Equals(other.Zoom)
        && SelectedNodeIds.SequenceEqual(other.SelectedNodeIds, StringComparer.Ordinal);
}
