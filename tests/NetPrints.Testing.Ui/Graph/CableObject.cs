using NetPrints.Editor;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Testing.Ui.Driving;
using NetPrints.Testing.Ui.Snapshots;

namespace NetPrints.Testing.Ui.Graph;

/// <summary>
/// Component object of a cable. A cable's element spans the canvas, so gestures aim at the
/// midpoint between its two pins' connectors.
/// </summary>
public sealed class CableObject : UiElement
{
    private readonly GraphCanvas graph;
    private readonly string source;
    private readonly string sourcePin;
    private readonly string target;
    private readonly string targetPin;

    public CableObject(GraphCanvas graph, string name)
        : base(graph.Driver, new AutomationQuery(AutomationIds.Connection) { Within = graph.Query, Name = name })
    {
        this.graph = graph;
        var ends = name.Split("->");
        (source, sourcePin) = Split(ends[0]);
        (target, targetPin) = Split(ends[1]);
    }

    private static (string Node, string Pin) Split(string end)
    {
        int dot = end.IndexOf('.', StringComparison.Ordinal);
        return (end[..dot], end[(dot + 1)..]);
    }

    /// <summary>The point midway between the two connectors.</summary>
    public Task<UiTarget> MidpointAsync(CancellationToken cancellationToken) => PointAlongAsync(0.5, cancellationToken);

    /// <summary>The point on the straight line between the two connectors, 0 being the source's and 1 the target's.</summary>
    /// <param name="fraction">How far from the source connector.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The point.</returns>
    public async Task<UiTarget> PointAlongAsync(double fraction, CancellationToken cancellationToken)
    {
        var a = await graph.Node(source).Output(sourcePin).Connector.CenterAsync(cancellationToken);
        var b = await graph.Node(target).Input(targetPin).Connector.CenterAsync(cancellationToken);
        return new UiTarget(a.Window, a.X + ((b.X - a.X) * fraction), a.Y + ((b.Y - a.Y) * fraction));
    }

    public new async Task ClickAsync(UiButton button, CancellationToken cancellationToken) =>
        await Driver.ClickAsync(await MidpointAsync(cancellationToken), button, 1, cancellationToken);

    public new async Task DoubleClickAsync(CancellationToken cancellationToken) =>
        await Driver.ClickAsync(await MidpointAsync(cancellationToken), UiButton.Left, 2, cancellationToken);

    public new async Task HoverAsync(CancellationToken cancellationToken) =>
        await Driver.MoveAsync(await MidpointAsync(cancellationToken), cancellationToken);

    /// <summary>The pixels around the midpoint (a window screenshot cut to <paramref name="size"/> x <paramref name="size"/>).</summary>
    public async Task<UiImage> ScreenshotAroundMidpointAsync(int size, CancellationToken cancellationToken)
    {
        var mid = await MidpointAsync(cancellationToken);
        var image = await Driver.ScreenshotAsync(mid.Window, cancellationToken);
        return image.Crop((int)mid.X - size / 2, (int)mid.Y - size / 2, size, size);
    }
}
