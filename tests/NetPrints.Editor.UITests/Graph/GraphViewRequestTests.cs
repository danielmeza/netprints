using Avalonia.Headless.XUnit;
using NetPrints.Editor.Graph;
using NetPrints.Editor.UITests.Driving;
using NetPrints.Editor.UITests.Shell;
using NetPrints.Testing.Ui.Driving;

namespace NetPrints.Editor.UITests.Graph;

/// <summary>The canvas carries out the requests the graph view model raises for the viewport and the node search.</summary>
public class GraphViewRequestTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Task UntilViewportLeavesAsync(EditorSession session, (double X, double Y) from) =>
        UiWait.UntilAsync(session.Driver, async () => await session.Graph.ViewportAsync(Token) != from, "the viewport moved", Token);

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task FitAllMovesTheViewportToTheNodes()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        await session.Graph.RightDragAsync(300, 200, Token);
        var panned = await session.Graph.ViewportAsync(Token);

        session.GraphViewModel.RequestView(GraphViewRequest.FitAll);

        await UntilViewportLeavesAsync(session, panned);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task FrameSelectionMovesTheViewportToTheSelectedNodes()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        await session.Graph.RightDragAsync(300, 200, Token);
        var panned = await session.Graph.ViewportAsync(Token);
        session.GraphViewModel.SelectNodes([session.GraphViewModel.Nodes.Last()], deselectPrevious: true);

        session.GraphViewModel.RequestView(GraphViewRequest.FrameSelection);

        await UntilViewportLeavesAsync(session, panned);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task NodeSearchRequestOpensTheSearch()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        Assert.False(await session.Graph.Search.IsOpenAsync(Token));

        session.GraphViewModel.RequestView(GraphViewRequest.NodeSearch);

        await session.Graph.Search.WaitOpenAsync(Token);
    }
}
