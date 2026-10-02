using Avalonia.Headless.XUnit;
using NetPrints.Editor.Graph;
using NetPrints.Editor.UITests.Driving;
using NetPrints.Editor.UITests.Shell;
using NetPrints.Testing.Ui.Driving;

namespace NetPrints.Editor.UITests.Graph;

/// <summary>Opening a graph puts focus in its canvas, so the graph shortcuts work without a click (SC-004).</summary>
public class GraphKeyboardFocusTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<EditorSession> OpenByPickingAMethodAsync()
    {
        var session = await EditorSession.OpenSampleMainAsync(Token);
        await session.PickMainFromTheTreeAsync(Token);
        return session;
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task CtrlSpaceRightAfterPickingAMethodOpensTheSearch()
    {
        await using var session = await OpenByPickingAMethodAsync();

        await session.Driver.PressAsync("Ctrl+Space", Token);

        await session.Graph.Search.WaitOpenAsync(Token);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public Task FRightAfterPickingAMethodFramesTheSelection() => ViewShortcutRaisesAsync("F", GraphViewRequest.FrameSelection);

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public Task HomeRightAfterPickingAMethodFitsAllNodes() => ViewShortcutRaisesAsync("Home", GraphViewRequest.FitAll);

    private static async Task ViewShortcutRaisesAsync(string chord, GraphViewRequest expected)
    {
        await using var session = await OpenByPickingAMethodAsync();
        session.GraphViewModel.SelectNodes([session.GraphViewModel.Nodes.Last()], deselectPrevious: true);
        List<GraphViewRequest> raised = [];
        session.GraphViewModel.ViewRequested += (_, request) => raised.Add(request);

        await session.Driver.PressAsync(chord, Token);

        Assert.Equal([expected], raised);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task DeleteRightAfterPickingAMethodDeletesTheSelectedNode()
    {
        await using var session = await OpenByPickingAMethodAsync();
        var node = session.GraphViewModel.Nodes.Last();
        session.GraphViewModel.SelectNodes([node], deselectPrevious: true);

        await session.Driver.PressAsync("Delete", Token);

        Assert.DoesNotContain(node, session.GraphViewModel.Nodes);
    }
}
