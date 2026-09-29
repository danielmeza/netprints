using System;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace NetPrints.Editor.Controls;

/// <summary>Raised by a <see cref="CanvasPopup"/> that is about to open with no pointer position tracked.</summary>
public sealed class CanvasPopupFallbackEventArgs : EventArgs
{
    /// <summary>The handler sets this to a window-relative point to anchor the popup at instead.</summary>
    public Point Position { get; set; }
}

/// <summary>
/// A <see cref="Popup"/> that anchors itself at the pointer instead of a caller-supplied screen
/// position, and centralizes the lifecycle behavior every canvas overlay needs (ADR-0004). Every
/// canvas popup (node/pin/connection context menus, tooltips, inline editors, choosers) uses this
/// instead of a raw <see cref="Popup"/>.
/// </summary>
public sealed class CanvasPopup : Popup
{
    private static readonly ConditionalWeakTable<TopLevel, CanvasPopup> OpenPopups = new();

    private Point? frozenAnchor;
    private IInputElement? restoreFocusTo;

    /// <summary>
    /// Raised when the popup is about to open and no pointer position is tracked for its window (a
    /// keyboard-triggered open): the handler sets <see cref="CanvasPopupFallbackEventArgs.Position"/>
    /// to a window-relative fallback point, such as the selected node or the canvas center.
    /// </summary>
    public event EventHandler<CanvasPopupFallbackEventArgs>? FallbackPositionRequested;

    /// <summary>Configures the placement and lifecycle behavior shared by every canvas popup.</summary>
    public CanvasPopup()
    {
        Placement = PlacementMode.Custom;
        CustomPopupPlacementCallback = OnCustomPlacement;
        IsLightDismissEnabled = true;
        Opened += OnOpened;
        Closed += OnClosed;
    }

    private void OnCustomPlacement(CustomPopupPlacement placement)
    {
        var topLevel = TopLevel.GetTopLevel(PlacementTarget ?? this);
        if (topLevel is null)
        {
            return;
        }

        frozenAnchor ??= CanvasPointerTracker.For(topLevel).LastPosition ?? RaiseFallbackPositionRequested();

        double maxX = Math.Max(0, topLevel.ClientSize.Width - placement.PopupSize.Width);
        double maxY = Math.Max(0, topLevel.ClientSize.Height - placement.PopupSize.Height);
        var anchor = new Point(Math.Clamp(frozenAnchor.Value.X, 0, maxX), Math.Clamp(frozenAnchor.Value.Y, 0, maxY));

        placement.AnchorRectangle = new Rect(anchor, new Size(1, 1));
        placement.Anchor = PopupAnchor.TopLeft;
        placement.Gravity = PopupGravity.BottomRight;
        placement.ConstraintAdjustment = PopupPositionerConstraintAdjustment.None;
        placement.Offset = default;
    }

    private Point RaiseFallbackPositionRequested()
    {
        var e = new CanvasPopupFallbackEventArgs();
        FallbackPositionRequested?.Invoke(this, e);
        return e.Position;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        frozenAnchor = null;

        var topLevel = TopLevel.GetTopLevel(PlacementTarget ?? this);
        if (topLevel is not null)
        {
            if (OpenPopups.TryGetValue(topLevel, out var other) && !ReferenceEquals(other, this))
            {
                other.IsOpen = false;
            }

            OpenPopups.AddOrUpdate(topLevel, this);
            restoreFocusTo = topLevel.FocusManager is { } focusManager ? focusManager.GetFocusedElement() : null;
        }

        Child?.AddHandler(KeyDownEvent, OnChildKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        Dispatcher.UIThread.Post(FocusFirst, DispatcherPriority.Input);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        Child?.RemoveHandler(KeyDownEvent, OnChildKeyDown);

        var topLevel = TopLevel.GetTopLevel(PlacementTarget ?? this);
        if (topLevel is not null && OpenPopups.TryGetValue(topLevel, out var current) && ReferenceEquals(current, this))
        {
            OpenPopups.Remove(topLevel);
        }

        restoreFocusTo?.Focus();
        restoreFocusTo = null;
    }

    private void OnChildKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            IsOpen = false;
            e.Handled = true;
        }
    }

    private void FocusFirst()
    {
        if (Child is { } child)
        {
            (FindFirstFocusable(child) ?? child).Focus();
        }
    }

    private static IInputElement? FindFirstFocusable(Visual visual)
    {
        foreach (var child in visual.GetVisualChildren())
        {
            if (child is InputElement { Focusable: true, IsEffectivelyEnabled: true } candidate)
            {
                return candidate;
            }

            if (FindFirstFocusable(child) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }
}
