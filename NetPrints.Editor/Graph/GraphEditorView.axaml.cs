using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using Nodify.Avalonia.Connections;
using Nodify.Avalonia.Events;
using NetPrints.Editor.Graph.Nodes;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Editor.Graph.Pins;

namespace NetPrints.Editor.Graph;

/// <summary>
/// The graph canvas on Nodify (PAR-38..57). Pointer gestures that Nodify does not provide are
/// handled here and forwarded to the view models.
/// </summary>
public partial class GraphEditorView : UserControl
{
    private const double ClickThreshold = 4;
    private Point? rightPressPosition;
    private object? backButtonTarget;
    private readonly DrawingBrush gridBrush;

    public GraphEditorView()
    {
        InitializeComponent();

        // 28-px grid that follows panning and zooming (PAR-38), lines tinted from the theme.
        IBrush gridLineBrush = Brushes.Gray;
        if (Application.Current?.TryGetResource("SystemControlForegroundBaseMediumLowBrush", Application.Current.ActualThemeVariant, out var resource) == true
            && resource is IBrush themeBrush)
        {
            gridLineBrush = themeBrush;
        }
        gridBrush = new DrawingBrush(new GeometryDrawing
        {
            Geometry = new RectangleGeometry(new Rect(0, 0, GraphConstants.GridCellSize, GraphConstants.GridCellSize)),
            Pen = new Pen(gridLineBrush, 0.5),
        })
        {
            TileMode = TileMode.Tile,
            DestinationRect = new RelativeRect(0, 0, GraphConstants.GridCellSize, GraphConstants.GridCellSize, RelativeUnit.Absolute),
        };
        Editor.Background = gridBrush;
        Editor.PropertyChanged += (_, e) =>
        {
            if (e.Property == Nodify.Avalonia.NodifyEditor.ViewportLocationProperty || e.Property == Nodify.Avalonia.NodifyEditor.ViewportZoomProperty)
            {
                UpdateGridTransform();
            }
        };

        Editor.AddHandler(PointerPressedEvent, OnEditorPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        Editor.AddHandler(PointerReleasedEvent, OnEditorPointerReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
        Editor.AddHandler(PointerMovedEvent, OnEditorPointerMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        Editor.AddHandler(PointerCaptureLostEvent, (_, _) => Editor.Cursor = null, RoutingStrategies.Bubble, handledEventsToo: true);
        Editor.AddHandler(Connector.PendingConnectionCompletedEvent, new PendingConnectionEventHandler(OnPendingConnectionCompleted),
            RoutingStrategies.Bubble, handledEventsToo: true);
        Editor.AddHandler(DragDrop.DragOverEvent, OnDragOver);
        Editor.AddHandler(DragDrop.DropEvent, OnDrop);

        SearchPopup.Opened += (_, _) => SearchView.FocusSearchBox();
    }

    public NodeGraphVM? ViewModel => DataContext as NodeGraphVM;

    /// <summary>Converts a point relative to the editor control to graph coordinates.</summary>
    public GraphPoint ToGraph(Point editorPoint)
    {
        var location = Editor.ViewportLocation;
        double zoom = Editor.ViewportZoom;
        return new GraphPoint(location.X + editorPoint.X / zoom, location.Y + editorPoint.Y / zoom);
    }

    /// <summary>Current pointer position in graph coordinates.</summary>
    public GraphPoint PointerGraphPosition => new(Editor.MouseLocation.X, Editor.MouseLocation.Y);

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        // Opening another graph resets the view (PAR-51).
        Editor.ViewportZoom = 1;
        Editor.ViewportLocation = new Point(0, 0);
        UpdateGridTransform();
    }

    private void UpdateGridTransform()
    {
        var location = Editor.ViewportLocation;
        double zoom = Editor.ViewportZoom;
        gridBrush.Transform = new MatrixTransform(Matrix.CreateTranslation(-location.X, -location.Y) * Matrix.CreateScale(zoom, zoom));
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

        if (properties.IsLeftButtonPressed && e.ClickCount == 2 && FindContext<ConnectionVM>(e.Source) is { } doubleClicked)
        {
            // Double click on a cable inserts a reroute node midway (PAR-48).
            doubleClicked.InsertReroute();
            e.Handled = true;
            return;
        }

        if (properties.IsXButton1Pressed)
        {
            backButtonTarget = (object?)FindContext<ConnectionVM>(e.Source) ?? FindContext<NodePinVM>(e.Source);
            return;
        }

        if (properties.IsMiddleButtonPressed)
        {
            // Middle click: clear an unconnected value (PAR-44), disconnect a pin or cable (PAR-48).
            if (FindContext<NodePinVM>(e.Source) is { } pin)
            {
                if (IsInsideValueEditor(e.Source))
                {
                    pin.ClearUnconnectedValue();
                }
                else
                {
                    pin.DisconnectAll();
                }

                e.Handled = true;
            }
            else if (FindContext<ConnectionVM>(e.Source) is { } connection)
            {
                connection.Disconnect();
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
                _ = graph.OpenSearchAsync(ToGraph(released));
                e.Handled = true;
            }
        }
    }

    private void OnPendingConnectionCompleted(object? sender, PendingConnectionEventArgs e)
    {
        if (e.Canceled || ViewModel is not { } graph || e.SourceConnector is not NodePinVM source)
        {
            return;
        }

        if (e.TargetConnector is NodePinVM target)
        {
            // Connects only compatible pins (PAR-46).
            graph.Connect(source, target);
        }
        else
        {
            // Released on empty canvas: search filtered for the pin, then auto-connect (PAR-47).
            _ = graph.OpenSearchAsync(PointerGraphPosition, source.Pin);
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Contains(GraphDragDrop.MethodFormat) || e.DataTransfer.Contains(GraphDragDrop.VariableFormat)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (ViewModel is not { } graph)
        {
            return;
        }

        var position = ToGraph(e.GetPosition(Editor));

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
    }
}
