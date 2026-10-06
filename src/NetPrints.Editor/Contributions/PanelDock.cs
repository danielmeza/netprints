namespace NetPrints.Editor.Contributions;

/// <summary>Where a panel docks by default.</summary>
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
