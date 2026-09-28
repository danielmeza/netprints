using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using NetPrints.Editor.Controls;
using NetPrints.Editor.Graph.Nodes;
using NetPrints.Editor.Graph.Pins;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Hosting.Automation;
using Nodify.Avalonia;
using Nodify.Avalonia.Connections;
using Nodify.Avalonia.Helpers.Gestures;

namespace NetPrints.Editor.Graph;

/// <summary>
/// The graph canvas on Nodify (PAR-38..57). Connect and reroute-insert go through Nodify's own
/// commands (bound in the XAML, ED-T09); disconnect uses button state, not Nodify's click-counted
/// gesture, so a middle click right after another click still registers; other pointer gestures
/// Nodify does not provide are handled here and forwarded to the view models.
/// </summary>
public partial class GraphEditorView : UserControl
{
    private const double ClickThreshold = 4;
    private const double HalfDivisor = 2;
    private Point? rightPressPosition;
    private object? backButtonTarget;
    private TopLevel? keyboardTopLevel;
    private NodeGraphVM? revealSubscription;

    /// <summary>
    /// Drops Nodify's keyboard alternates for starting/ending a pin connection and for
    /// toggling/panning by keyboard (OWN-06): a <c>KeyDown</c> that bubbles up from a pin's value
    /// or name text box reaches the pin's connector before it reaches anything of ours, so Space
    /// (the connector's "Connect" gesture) moved focus off the text box onto the connector and
    /// showed Nodify's keyboard-connect hotkey badges ("some numbers show up on the nodes")
    /// instead of inserting a space, and Delete (the connector's "Disconnect" gesture)
    /// disconnected the pin instead of deleting a character. Pins are only ever connected or
    /// disconnected by dragging in this app (PAR-46, PAR-48), and the editor is never panned or
    /// selected by keyboard, so these keyboard-only alternates are removed once, globally; the
    /// mouse gestures they pair with are untouched.
    /// </summary>
    static GraphEditorView()
    {
        EditorGestures.Mappings.Connector.Connect.Value = new PointerGesture(MouseAction.LeftClick);
        EditorGestures.Mappings.Connector.Disconnect.Value = new PointerGesture(MouseAction.LeftClick, KeyModifiers.Alt);
        EditorGestures.Mappings.Editor.Keyboard.ToggleSelected.Unbind();
        EditorGestures.Mappings.Editor.Keyboard.Pan.Unbind();
    }

    /// <summary>
    /// Loads the control's XAML and wires the pointer and drag/drop handlers Nodify does not
    /// provide, plus keeping the background grid's viewport synced to the editor's.
    /// </summary>
    public GraphEditorView()
    {
        InitializeComponent();

        SyncGrid();
        Editor.PropertyChanged += (_, e) =>
        {
            if (e.Property == Nodify.Avalonia.NodifyEditor.ViewportLocationProperty || e.Property == Nodify.Avalonia.NodifyEditor.ViewportZoomProperty)
            {
                SyncGrid();
            }
        };

        Editor.AddHandler(PointerPressedEvent, OnEditorPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        Editor.AddHandler(PointerReleasedEvent, OnEditorPointerReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
        Editor.AddHandler(PointerMovedEvent, OnEditorPointerMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        Editor.AddHandler(PointerCaptureLostEvent, (_, _) => Editor.Cursor = null, RoutingStrategies.Bubble, handledEventsToo: true);
        Editor.AddHandler(DragDrop.DragOverEvent, OnDragOver);
        Editor.AddHandler(DragDrop.DropEvent, OnDrop);

        SearchPopup.Opened += (_, _) => SearchView.FocusSearchBox();
        SearchPopup.FallbackPositionRequested += (_, e) => e.Position = FallbackScreenPosition();
        GetSetPopup.FallbackPositionRequested += (_, e) => e.Position = FallbackScreenPosition();

        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        keyboardTopLevel = TopLevel.GetTopLevel(this);
        keyboardTopLevel?.AddHandler(KeyDownEvent, OnGlobalKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);

        // Attaches the tracker now, not lazily on first popup open, so it does not miss the very
        // pointer event that triggers that first open.
        if (keyboardTopLevel is { } topLevel)
        {
            CanvasPointerTracker.For(topLevel);
        }
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        keyboardTopLevel?.RemoveHandler(KeyDownEvent, OnGlobalKeyDown);
        keyboardTopLevel = null;
    }

    /// <summary>
    /// Ctrl+Space opens the node search without the pointer (ADR-0004): the popup falls back to the
    /// selected node's position, or the canvas center if nothing is selected.
    /// </summary>
    private void OnGlobalKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Space || e.KeyModifiers != KeyModifiers.Control || ViewModel is not { } graph)
        {
            return;
        }

        if (TopLevel.GetTopLevel(this) is { } topLevel)
        {
            CanvasPointerTracker.For(topLevel).Invalidate();
        }

        // Not a command, and OpenSearchAsync has no catch of its own: route a fault to the error dialog too.
        graph.OpenSearchAsync(FallbackGraphPosition()).Forget(graph.Context, "Failed to open the node search");
        e.Handled = true;
    }

    /// <summary>The bound graph view model, or <see langword="null"/> if the data context is not one.</summary>
    public NodeGraphVM? ViewModel => DataContext as NodeGraphVM;

    /// <summary>Converts a point relative to the editor control to graph coordinates.</summary>
    public GraphPoint ToGraph(Point editorPoint)
    {
        var location = Editor.ViewportLocation;
        double zoom = Editor.ViewportZoom;
        return new GraphPoint(location.X + editorPoint.X / zoom, location.Y + editorPoint.Y / zoom);
    }

    /// <summary>Converts a graph position to a point relative to the editor control (the inverse of <see cref="ToGraph"/>).</summary>
    private Point ToScreen(GraphPoint graphPoint)
    {
        var location = Editor.ViewportLocation;
        double zoom = Editor.ViewportZoom;
        return new Point((graphPoint.X - location.X) * zoom, (graphPoint.Y - location.Y) * zoom);
    }

    private NodeVM? SelectedNode => ViewModel?.SelectedNodes.FirstOrDefault();

    /// <summary>The canvas center, relative to the editor control.</summary>
    private Point CanvasCenterPoint => new(Editor.Bounds.Width / HalfDivisor, Editor.Bounds.Height / HalfDivisor);

    /// <summary>
    /// Where a popup falls back to when there is no tracked pointer position (ADR-0004): the
    /// selected node's position, or the canvas center, in the top level's coordinates.
    /// </summary>
    private Point FallbackScreenPosition()
    {
        var editorPoint = SelectedNode is { } selected ? ToScreen(selected.Location) : CanvasCenterPoint;
        var topLevel = TopLevel.GetTopLevel(this);
        return topLevel is null ? editorPoint : Editor.TranslatePoint(editorPoint, topLevel) ?? editorPoint;
    }

    /// <summary>Where a keyboard-triggered node search creates its node (ADR-0004): the selected node's position, or the canvas center.</summary>
    private GraphPoint FallbackGraphPosition() => SelectedNode?.Location ?? ToGraph(CanvasCenterPoint);

    /// <summary>Resets the viewport to zoom 1 and the origin when a new graph is opened (PAR-51),
    /// and follows the new graph's <see cref="NodeGraphVM.NodeRevealRequested"/> (FR-034, ED-T03).</summary>
    /// <param name="e">Unused; forwarded to the base implementation.</param>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (revealSubscription is { } previous)
        {
            previous.NodeRevealRequested -= OnNodeRevealRequested;
        }

        revealSubscription = ViewModel;
        if (revealSubscription is { } current)
        {
            current.NodeRevealRequested += OnNodeRevealRequested;
        }

        // Opening another graph resets the view (PAR-51).
        Editor.ViewportZoom = 1;
        Editor.ViewportLocation = new Point(0, 0);
    }

    /// <summary>Centers the viewport on a revealed node (FR-034, ED-T03).</summary>
    private void OnNodeRevealRequested(object? sender, NodeVM node)
    {
        var center = CanvasCenterPoint;
        double zoom = Editor.ViewportZoom;
        Editor.ViewportLocation = new Point(node.Location.X - center.X / zoom, node.Location.Y - center.Y / zoom);
    }

    private void SyncGrid()
    {
        Grid.ViewportLocation = Editor.ViewportLocation;
        Grid.ViewportZoom = Editor.ViewportZoom;
    }

    private static T? FindContext<T>(object? source) where T : class
    {
        for (var visual = source as Visual; visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is StyledElement { DataContext: T match })
            {
                return match;
            }

            if (visual is StyledElement { DataContext: NodeVM or NodeGraphVM })
            {
                return null;
            }
        }

        return null;
    }

    private static bool IsInsideValueEditor(object? source)
    {
        for (var visual = source as Visual; visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is TextBox or CheckBox or ComboBox)
            {
                return true;
            }

            if (visual is Connector)
            {
                return false;
            }
        }

        return false;
    }

    private void OnEditorPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var properties = e.GetCurrentPoint(Editor).Properties;

        // The editor captures the pointer, so gesture targets are resolved on press.
        if (properties.IsRightButtonPressed)
        {
            rightPressPosition = FindContext<NodeVM>(e.Source) is null ? e.GetPosition(Editor) : null;
            return;
        }

        if (properties.IsXButton1Pressed)
        {
            backButtonTarget = (object?)FindContext<ConnectionVM>(e.Source) ?? FindContext<NodePinVM>(e.Source);
            return;
        }

        if (properties.IsMiddleButtonPressed)
        {
            // Middle click: clear an unconnected value (PAR-44), disconnect a pin or cable (PAR-48),
            // through the pin's/connection's own command (ED-T09).
            if (FindContext<NodePinVM>(e.Source) is { } pin)
            {
                if (IsInsideValueEditor(e.Source))
                {
                    pin.ClearUnconnectedValueCommand.Execute(null);
                }
                else
                {
                    pin.DisconnectAllCommand.Execute(null);
                }

                e.Handled = true;
            }
            else if (FindContext<ConnectionVM>(e.Source) is { } connection)
            {
                connection.DisconnectCommand.Execute(null);
                e.Handled = true;
            }
        }
    }

    private void OnEditorPointerMoved(object? sender, PointerEventArgs e)
    {
        // A right drag past the click threshold pans: show the move cursor (PAR-51).
        if (rightPressPosition is { } pressed && e.GetCurrentPoint(Editor).Properties.IsRightButtonPressed)
        {
            var position = e.GetPosition(Editor);
            if (Math.Abs(position.X - pressed.X) >= ClickThreshold || Math.Abs(position.Y - pressed.Y) >= ClickThreshold)
            {
                Editor.Cursor = EditorCursors.Move;
            }
        }
    }

    private void OnEditorPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton == MouseButton.XButton1)
        {
            // Mouse back button toggles "faint" on a cable or a connected pin (PAR-48).
            switch (backButtonTarget)
            {
                case ConnectionVM connection:
                    connection.ToggleFaint();
                    e.Handled = true;
                    break;
                case NodePinVM { IsConnected: true } pin:
                    pin.ToggleFaint();
                    e.Handled = true;
                    break;
            }

            backButtonTarget = null;
            return;
        }

        if (e.InitialPressMouseButton == MouseButton.Right)
        {
            Editor.Cursor = null;
        }

        if (e.InitialPressMouseButton == MouseButton.Right && rightPressPosition is { } pressed)
        {
            rightPressPosition = null;
            var released = e.GetPosition(Editor);

            // Right click without dragging opens the node search; right drag pans (PAR-51, 52).
            if (Math.Abs(released.X - pressed.X) < ClickThreshold && Math.Abs(released.Y - pressed.Y) < ClickThreshold
                && ViewModel is { } graph)
            {
                // Not a command, and OpenSearchAsync has no catch of its own: route a fault to the error dialog too.
                graph.OpenSearchAsync(ToGraph(released)).Forget(graph.Context, "Failed to open the node search");
                e.Handled = true;
            }
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Contains(GraphDragDrop.MethodFormat)
            || e.DataTransfer.Contains(GraphDragDrop.VariableFormat)
            || e.DataTransfer.Contains(GraphDragDrop.LocalVariableFormat)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (ViewModel is not { } graph)
        {
            return;
        }

        var screenPosition = e.GetPosition(Editor);
        var position = ToGraph(screenPosition);

        if (e.DataTransfer.TryGetValue(GraphDragDrop.MethodFormat) is { } method)
        {
            graph.Drop(method, position);
            e.Handled = true;
        }
        else if (e.DataTransfer.TryGetValue(GraphDragDrop.VariableFormat) is { } variable)
        {
            graph.Drop(variable, position);
            e.Handled = true;
        }
        else if (e.DataTransfer.TryGetValue(GraphDragDrop.LocalVariableFormat) is { } local)
        {
            graph.Drop(local, position);
            e.Handled = true;
        }
    }
}
