using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace NetPrints.Editor.Controls;

/// <summary>
/// Tracks the last pointer position within a <see cref="TopLevel"/>, in that top level's own
/// coordinates, so a <see cref="CanvasPopup"/> can anchor to it (ADR-0004). Avalonia's built-in
/// pointer tracking (used by <c>Placement="Pointer"</c>) is internal to Avalonia and, per commit
/// 4b8e580, is not kept current by drag-and-drop, so this listens for pointer and drag events
/// itself, once per <see cref="TopLevel"/>.
/// </summary>
internal sealed class CanvasPointerTracker
{
    private static readonly ConditionalWeakTable<TopLevel, CanvasPointerTracker> Trackers = new();

    private Point? lastPosition;

    private CanvasPointerTracker(TopLevel topLevel)
    {
        topLevel.AddHandler(InputElement.PointerMovedEvent, (_, e) => lastPosition = e.GetPosition(topLevel), RoutingStrategies.Tunnel, handledEventsToo: true);
        topLevel.AddHandler(InputElement.PointerPressedEvent, (_, e) => lastPosition = e.GetPosition(topLevel), RoutingStrategies.Tunnel, handledEventsToo: true);

        // Drag-and-drop raises DragEventArgs, not PointerEventArgs, so the handlers above miss it:
        // this is what keeps a variable drop anchored correctly (the owner-reported bug).
        topLevel.AddHandler(DragDrop.DragOverEvent, (_, e) => lastPosition = e.GetPosition(topLevel), RoutingStrategies.Bubble, handledEventsToo: true);
        topLevel.AddHandler(DragDrop.DropEvent, (_, e) => lastPosition = e.GetPosition(topLevel), RoutingStrategies.Bubble, handledEventsToo: true);
    }

    /// <summary>The tracker for <paramref name="topLevel"/>, creating and attaching it on first use.</summary>
    public static CanvasPointerTracker For(TopLevel topLevel) => Trackers.GetValue(topLevel, static tl => new CanvasPointerTracker(tl));

    /// <summary>The last pointer position seen, in <see cref="TopLevel"/> coordinates, or null if none was seen since the last <see cref="Invalidate"/>.</summary>
    public Point? LastPosition => lastPosition;

    /// <summary>
    /// Clears the tracked position, so the next <see cref="CanvasPopup"/> to open falls back to its
    /// own default placement instead of the (now stale) last pointer position. Called by a
    /// keyboard-triggered open, which was not caused by the pointer at all.
    /// </summary>
    public void Invalidate() => lastPosition = null;
}
