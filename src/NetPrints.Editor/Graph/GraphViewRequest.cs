namespace NetPrints.Editor.Graph;

/// <summary>A viewport or popup action that only the canvas can carry out (the view model raises it, a behavior on the editor does it).</summary>
public enum GraphViewRequest
{
    /// <summary>Scroll and zoom so the selected nodes fill the viewport.</summary>
    FrameSelection,

    /// <summary>Scroll and zoom so every node fills the viewport.</summary>
    FitAll,

    /// <summary>Open the node search at the selected node, or at the canvas center.</summary>
    NodeSearch,
}
