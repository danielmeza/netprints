using System.Globalization;
using NetPrints.Editor;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Testing.Ui.Driving;
using NetPrints.Testing.Ui.Search;

namespace NetPrints.Testing.Ui.Graph;

/// <summary>Component object of the graph canvas (Nodify editor) of a class window.</summary>
public sealed class GraphCanvas(IUiDriver driver, AutomationQuery window)
    : UiElement(driver, new AutomationQuery(AutomationIds.GraphEditor) { Within = window })
{
    public AutomationQuery WindowQuery { get; } = window;

    /// <summary>The background grid behind the canvas.</summary>
    public UiElement Grid => new(Driver, new AutomationQuery(AutomationIds.GraphGrid) { Within = WindowQuery });

    public UiElement Watermark => new(Driver, new AutomationQuery(AutomationIds.GraphWatermark) { Within = WindowQuery });

    public SearchPopup Search => new(Driver, WindowQuery);

    public GetSetChooser GetSet => new(Driver, WindowQuery);

    /// <summary>A node by its name (the node's type name, e.g. "CallMethodNode"), the n-th when several share it.</summary>
    public NodeObject Node(string name, int index = 0) =>
        new(Driver, new AutomationQuery(AutomationIds.Node) { Within = Query, Name = name, Index = index });

    /// <summary>A cable by its identity "&lt;node&gt;.&lt;pin&gt;-&gt;&lt;node&gt;.&lt;pin&gt;".</summary>
    public CableObject Connection(string name) => new(this, name);

    public async Task<IReadOnlyList<string>> NodeNamesAsync(CancellationToken cancellationToken) =>
        (await Driver.FindAllAsync(new AutomationQuery(AutomationIds.Node) { Within = Query }, cancellationToken)).Select(e => e.Name ?? "").ToList();

    public async Task<IReadOnlyList<string>> ConnectionNamesAsync(CancellationToken cancellationToken) =>
        (await Driver.FindAllAsync(new AutomationQuery(AutomationIds.Connection) { Within = Query }, cancellationToken)).Select(e => e.Name ?? "").ToList();

    public async Task<int> NodeCountAsync(CancellationToken cancellationToken) => (await NodeNamesAsync(cancellationToken)).Count;

    /// <summary>Waits until the graph <paramref name="name"/> is shown and its nodes and cables stopped changing.</summary>
    public async Task<GraphCanvas> WaitForGraphAsync(string name, CancellationToken cancellationToken)
    {
        await Watermark.WaitUntilAsync(e => e.Text == name, $"graph '{name}' shown", cancellationToken);
        await WaitRenderedAsync(cancellationToken);
        return this;
    }

    /// <summary>Waits until the nodes and cables on the canvas are the same on two consecutive polls.</summary>
    public async Task WaitRenderedAsync(CancellationToken cancellationToken)
    {
        string? last = null;
        await UiWait.UntilAsync(Driver, async () =>
        {
            string now = string.Join('|', await NodeNamesAsync(cancellationToken)) + "#" + string.Join('|', await ConnectionNamesAsync(cancellationToken));
            bool stable = now == last && !now.StartsWith('#');
            last = now;
            return stable;
        }, "nodes and cables rendered", cancellationToken);
    }

    public async Task<double> ZoomAsync(CancellationToken cancellationToken) => await GetAsync<double>("ViewportZoom", cancellationToken);

    public async Task<(double X, double Y)> ViewportAsync(CancellationToken cancellationToken)
    {
        var e = await GetAsync(cancellationToken);
        return (double.Parse(e["ViewportX"]!, CultureInfo.InvariantCulture), double.Parse(e["ViewportY"]!, CultureInfo.InvariantCulture));
    }

    /// <summary>Converts a point on the canvas to graph coordinates.</summary>
    public async Task<(double X, double Y)> ToGraphAsync(UiTarget at, CancellationToken cancellationToken)
    {
        var origin = await OffsetAsync(0, 0, cancellationToken);
        var (x, y) = await ViewportAsync(cancellationToken);
        double zoom = await ZoomAsync(cancellationToken);
        return (x + (at.X - origin.X) / zoom, y + (at.Y - origin.Y) / zoom);
    }

    public async Task<double> GridCellSizeAsync(CancellationToken cancellationToken) => await GetAsync<double>("GridCellSize", cancellationToken);

    /// <summary>The render path ("Shader", "Cpu" or "None") that drew the grid's latest frame.</summary>
    public async Task<string?> GridRenderPathAsync(CancellationToken cancellationToken) => await Grid.PropertyAsync("GridRenderPath", cancellationToken);

    /// <summary>The viewport the grid draws (it must follow the canvas's).</summary>
    public async Task<(double X, double Y, double Zoom)> GridViewportAsync(CancellationToken cancellationToken)
    {
        var e = await Grid.GetAsync(cancellationToken);
        return (double.Parse(e["ViewportX"]!, CultureInfo.InvariantCulture), double.Parse(e["ViewportY"]!, CultureInfo.InvariantCulture),
            double.Parse(e["ViewportZoom"]!, CultureInfo.InvariantCulture));
    }

    /// <summary>The grid's background, minor and major line colors, as #aarrggbb.</summary>
    public async Task<(string? Background, string? Minor, string? Major)> GridColorsAsync(CancellationToken cancellationToken)
    {
        var e = await Grid.GetAsync(cancellationToken);
        return (e["BackgroundColor"], e["MinorColor"], e["MajorColor"]);
    }

    /// <summary>The cursor the canvas shows (null: the default arrow).</summary>
    public async Task<string?> CursorAsync(CancellationToken cancellationToken) => await PropertyAsync("Cursor", cancellationToken);

    /// <summary>A point on the canvas away from the nodes: <c>150</c> DIPs in from the bottom-right corner, moved by (dx, dy).</summary>
    public async Task<UiTarget> EmptyPointAsync(CancellationToken cancellationToken, double dx = 0, double dy = 0)
    {
        var e = await GetAsync(cancellationToken);
        return await OffsetAsync(e.Bounds.Width - 150 + dx, e.Bounds.Height - 150 + dy, cancellationToken);
    }

    public async Task ClickEmptyAsync(CancellationToken cancellationToken) =>
        await Driver.ClickAsync(await EmptyPointAsync(cancellationToken), UiButton.Left, 1, cancellationToken);

    /// <summary>Right-clicks empty canvas, which opens the node search (PAR-52).</summary>
    public async Task<SearchPopup> RightClickEmptyAsync(CancellationToken cancellationToken)
    {
        await Driver.ClickAsync(await EmptyPointAsync(cancellationToken), UiButton.Right, 1, cancellationToken);
        return Search;
    }

    /// <summary>Right-clicks the canvas at (x, y) DIPs from its top-left corner (empty there), which opens the node search.</summary>
    public async Task<SearchPopup> RightClickAtAsync(double x, double y, CancellationToken cancellationToken)
    {
        await Driver.ClickAsync(await OffsetAsync(x, y, cancellationToken), UiButton.Right, 1, cancellationToken);
        return Search;
    }

    /// <summary>Right-drags on empty canvas, which pans (PAR-51).</summary>
    public async Task RightDragAsync(double dx, double dy, CancellationToken cancellationToken)
    {
        var from = await EmptyPointAsync(cancellationToken, -200, -200);
        await Driver.DragAsync(from, from.Offset(dx, dy), UiButton.Right, cancellationToken);
    }

    /// <summary>Presses the right button on empty canvas and moves, without releasing.</summary>
    public async Task<UiTarget> BeginRightDragAsync(double dx, double dy, CancellationToken cancellationToken)
    {
        var from = await EmptyPointAsync(cancellationToken, -200, -200);
        var to = from.Offset(dx, dy);
        await Driver.PressAndMoveAsync(from, to, UiButton.Right, cancellationToken);
        return to;
    }

    public async Task WheelAsync(UiTarget at, double delta, CancellationToken cancellationToken) => await Driver.WheelAsync(at, delta, cancellationToken);

    /// <summary>Drags a selection rectangle over the whole canvas.</summary>
    public async Task BoxSelectAllAsync(CancellationToken cancellationToken)
    {
        var e = await GetAsync(cancellationToken);
        await Driver.DragAsync(await OffsetAsync(5, 5, cancellationToken), await OffsetAsync(e.Bounds.Width - 5, e.Bounds.Height - 5, cancellationToken),
            UiButton.Left, cancellationToken);
    }
}
