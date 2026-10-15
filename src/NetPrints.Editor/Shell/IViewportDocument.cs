using NetPrints.Editor.Graph;

namespace NetPrints.Editor.Shell;

/// <summary>A document with a canvas whose viewport the session keeps: where it looks and how far it is zoomed.</summary>
internal interface IViewportDocument
{
    /// <summary>Gets or sets the canvas location in graph units.</summary>
    GraphPoint ViewportLocation { get; set; }

    /// <summary>Gets or sets the canvas zoom, 1 being full size.</summary>
    double ViewportZoom { get; set; }
}
