using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Nodify.Avalonia;
using Nodify.Avalonia.Connections;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Graph.Nodes;
using NetPrints.Editor.Graph.Pins;
using NetPrints.Editor.UITests.Search;

namespace NetPrints.Editor.UITests.Graph;

/// <summary>Page object of the graph canvas (Nodify editor) of a class window.</summary>
public sealed class GraphCanvasPage(Window window, GraphEditorView view)
{
    public Window Window { get; } = window;
    public GraphEditorView View { get; } = view;
    public NodeGraphVM ViewModel => (NodeGraphVM)View.DataContext!;
    public NodifyEditor Editor => View.ById<NodifyEditor>(AutomationIds.GraphEditor);
    public string? Watermark => View.ById<TextBlock>(AutomationIds.GraphWatermark).Text;
    public int RealizedNodeCount => Window.Descendants<ItemContainer>().Count();
    public int RealizedConnectionCount => Window.Descendants<Connection>().Count();

    public NodeSearchPage Search => new(Window, View.ById<Popup>(AutomationIds.GraphSearchPopup), ViewModel.Search);

    /// <summary>Waits until every node and cable of the view model is realized.</summary>
    public Task WaitForRenderedAsync() =>
        HeadlessInput.WaitUntilAsync(() => RealizedNodeCount == ViewModel.Nodes.Count && RealizedConnectionCount == ViewModel.Connections.Count,
            "nodes and cables realized");

    public ItemContainer ContainerOf(NodeVM node) => Window.Descendants<ItemContainer>().Single(c => c.DataContext == node);

    public Connector ConnectorOf(NodePinVM pin) => Window.Descendants<Connector>().Single(c => c.DataContext == pin);

    /// <summary>Center of a pin's connector shape in window coordinates.</summary>
    public Point PinPoint(NodePinVM pin) => ConnectorOf(pin).Descendants<Shape>().First(s => s.IsVisible).CenterIn(Window);

    /// <summary>Midpoint of a cable between its two pins.</summary>
    public Point CablePoint(ConnectionVM connection)
    {
        var a = PinPoint(connection.Source);
        var b = PinPoint(connection.Target);
        return new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2);
    }

    /// <summary>A point on the canvas away from the nodes (window coordinates).</summary>
    public Point EmptyPoint(double dx = 0, double dy = 0)
    {
        var origin = Origin;
        return new Point(origin.X + Editor.Bounds.Width - 150 + dx, origin.Y + Editor.Bounds.Height - 150 + dy);
    }

    public Point Origin => Editor.TranslatePoint(new Point(0, 0), Window)!.Value;

    public Point NodeLabelPoint(NodeVM node) =>
        ContainerOf(node).AllById<TextBlock>(AutomationIds.NodeLabel).Single().CenterIn(Window);

    public T NodeControl<T>(NodeVM node, string automationId) where T : Control => ContainerOf(node).ById<T>(automationId);

    public void DragPin(NodePinVM from, Point to) => Window.Drag(PinPoint(from), to);

    public void DragPin(NodePinVM from, NodePinVM to) => Window.Drag(PinPoint(from), PinPoint(to));

    public void ClickNode(NodeVM node) => Window.Click(NodeLabelPoint(node));

    public void DragNode(NodeVM node, Vector by)
    {
        var start = NodeLabelPoint(node);
        Window.Drag(start, start + by);
    }

    public void BoxSelectAll() =>
        Window.Drag(new Point(Origin.X + 5, Origin.Y + 5), new Point(Origin.X + Editor.Bounds.Width - 5, Origin.Y + Editor.Bounds.Height - 5));

    public void ClickEmpty() => Window.Click(EmptyPoint());

    public void RightClickEmpty() => Window.Click(EmptyPoint(), MouseButton.Right);

    public void RightDrag(Vector by)
    {
        var start = EmptyPoint(-200, -200);
        Window.Drag(start, start + by, MouseButton.Right);
    }

    public void Zoom(Point at, double delta) => Window.Wheel(at, delta);

    public GraphPoint ToGraph(Point windowPoint) =>
        View.ToGraph(Window.TranslatePoint(windowPoint, Editor) ?? throw new InvalidOperationException());

    public Button GetButton => View.ById<Button>(AutomationIds.GetButton);

    public Button SetButton => View.ById<Button>(AutomationIds.SetButton);

    public bool IsGetSetOpen => View.ById<Popup>(AutomationIds.GraphGetSetPopup).IsOpen;
}
