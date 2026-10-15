namespace NetPrints.Editor.Contributions;

/// <summary>Which <see cref="ContextMenuTarget"/> values a view renders a menu for today.</summary>
public static class ContextMenuTargets
{
    /// <summary>
    /// Whether a view opens a context menu for <paramref name="target"/>. The registry reports an item for a target
    /// without a surface instead of accepting an item nothing would show; <see cref="ContextMenuTarget.Node"/>,
    /// <see cref="ContextMenuTarget.Pin"/> and <see cref="ContextMenuTarget.Canvas"/> stay in the enum for the P3 surfaces.
    /// </summary>
    /// <param name="target">The target to test.</param>
    /// <returns><see langword="true"/> when a view consumes items of <paramref name="target"/>.</returns>
    public static bool HasSurface(ContextMenuTarget target) =>
        target is ContextMenuTarget.Connection or ContextMenuTarget.TreeClass or ContextMenuTarget.TreeMember or ContextMenuTarget.TreeEventGraph;
}
