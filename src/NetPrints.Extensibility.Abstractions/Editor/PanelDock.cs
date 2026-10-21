using System.Diagnostics.CodeAnalysis;
using NetPrints.Core;

namespace NetPrints.Extensibility.Editor;

/// <summary>Where a panel docks by default.</summary>
[Experimental(ExperimentalApiIds.EditorContributions, UrlFormat = ExperimentalApiIds.UrlFormat)]
public enum PanelDock
{
    /// <summary>The left dock.</summary>
    Left,

    /// <summary>The upper dock of the right column.</summary>
    Right,

    /// <summary>The lower dock of the right column, below <see cref="Right"/>.</summary>
    RightLower,

    /// <summary>The bottom dock.</summary>
    Bottom,
}
