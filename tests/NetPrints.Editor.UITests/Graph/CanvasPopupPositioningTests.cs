using Avalonia.Headless.XUnit;
using NetPrints.Core;
using NetPrints.Editor.Graph;
using NetPrints.Editor.UITests.ClassEditor;
using NetPrints.Graph;
using NetPrints.Testing.Ui.Driving;

namespace NetPrints.Editor.UITests.Graph;

/// <summary>
/// Every canvas popup anchors at the pointer, or a fallback when there is none (ADR-0004), one test
/// per opening path.
/// </summary>
public class CanvasPopupPositioningTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // Well clear of both the window's right/bottom edges (the search popup is 700x300) and of the
    // corner HoverAndDropTests's own drop-point test uses, so this exercises the unclamped case.
    private const double UnclampedDx = -450;
    private const double UnclampedDy = -50;

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task RightClickOpensSearchAtThePointer()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var at = await session.Graph.EmptyPointAsync(Token, UnclampedDx, UnclampedDy);

        await session.Driver.ClickAsync(at, UiButton.Right, 1, Token);
        var search = await session.Graph.Search.WaitOpenAsync(Token);

        var bounds = (await search.View.GetAsync(Token)).Bounds;
        Assert.Equal(at.X, bounds.X, 0);
        Assert.Equal(at.Y, bounds.Y, 0);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task ReleasingACableOnEmptyCanvasOpensSearchAtTheDropPoint()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var write = session.GraphViewModel.Nodes.Single(n => n.Node is CallMethodNode);
        var execOut = write.OutputExecPins.First(p => p.Pin.Name != "Catch");
        var at = await session.Graph.EmptyPointAsync(Token, UnclampedDx, UnclampedDy);

        await session.Graph.Node("CallMethodNode").Output(execOut.Pin.Name).DragCableToAsync(at, Token);
        var search = await session.Graph.Search.WaitOpenAsync(Token);

        var bounds = (await search.View.GetAsync(Token)).Bounds;
        Assert.Equal(at.X, bounds.X, 0);
        Assert.Equal(at.Y, bounds.Y, 0);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task PickingAPropertyFromMemberSearchAnchorsGetSetAtTheClickPoint()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var cls = session.GraphViewModel.Graph.Class ?? throw new InvalidOperationException("Main has no class.");
        var variable = new Variable(cls, "V", TypeSpecifier.FromType<Version>(), null, null, VariableModifiers.None);
        cls.Variables.Add(variable);
        session.GraphViewModel.AddNode<VariableGetterNode>(new GraphPoint(400, 100), null, variable.Specifier);
        await session.WaitForRenderedAsync(Token);

        // Drag from the object pin to empty canvas: opens the member search for its type (owner-reported bug).
        var dropPoint = await session.Graph.EmptyPointAsync(Token, UnclampedDx, UnclampedDy);
        await session.Graph.Node("VariableGetterNode").Output("Version").DragCableToAsync(dropPoint, Token);
        var search = await session.Graph.Search.WaitOpenAsync(Token);

        string rowText = await search.FilterAsync("major", t => t.Contains("Major", StringComparison.Ordinal), Token);
        var rowCenter = await search.Row(rowText).CenterAsync(Token);

        await search.ChooseAsync(rowText, Token);
        await session.Graph.GetSet.WaitOpenAsync(Token);

        var bounds = (await session.Graph.GetSet.View.GetAsync(Token)).Bounds;
        Assert.Equal(rowCenter.X, bounds.X, 0);
        Assert.Equal(rowCenter.Y, bounds.Y, 0);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task OpeningSearchByKeyboardFallsBackToTheSelectedNodeOrTheCanvasCenter()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var editorBounds = (await session.Graph.GetAsync(Token)).Bounds;

        // No selection: falls back to the canvas center.
        await session.Driver.PressAsync("Ctrl+Space", Token);
        var search = await session.Graph.Search.WaitOpenAsync(Token);
        var centerBounds = (await search.View.GetAsync(Token)).Bounds;
        Assert.Equal(editorBounds.X + editorBounds.Width / 2, centerBounds.X, 0);
        Assert.Equal(editorBounds.Y + editorBounds.Height / 2, centerBounds.Y, 0);

        await session.Driver.PressAsync("Escape", Token);
        await search.WaitClosedAsync(Token);

        // A node selected: falls back to its position.
        var node = session.Graph.Node("CallMethodNode");
        await node.SelectAsync(Token);
        var nodeLocation = session.GraphViewModel.Nodes.Single(n => n.Node is CallMethodNode).Location;

        await session.Driver.PressAsync("Ctrl+Space", Token);
        await search.WaitOpenAsync(Token);
        var nodeBounds = (await search.View.GetAsync(Token)).Bounds;
        Assert.Equal(editorBounds.X + nodeLocation.X, nodeBounds.X, 0);
        Assert.Equal(editorBounds.Y + nodeLocation.Y, nodeBounds.Y, 0);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task RightClickNearTheWindowEdgeClampsSearchInsideTheWindow()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var editorBounds = (await session.Graph.GetAsync(Token)).Bounds;
        var clientSize = session.ClassWindow.ClientSize;
        var at = await session.Graph.OffsetAsync(editorBounds.Width - 10, editorBounds.Height - 10, Token);

        await session.Driver.ClickAsync(at, UiButton.Right, 1, Token);
        var search = await session.Graph.Search.WaitOpenAsync(Token);

        var bounds = (await search.View.GetAsync(Token)).Bounds;
        Assert.Equal(clientSize.Width - bounds.Width, bounds.X, 0);
        Assert.Equal(clientSize.Height - bounds.Height, bounds.Y, 0);
        Assert.True(bounds.X >= 0 && bounds.Y >= 0, "the popup stays inside the window");
    }
}
