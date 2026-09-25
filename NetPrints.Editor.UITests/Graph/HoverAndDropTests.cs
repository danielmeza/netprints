using Avalonia.Headless.XUnit;
using Avalonia.Input;
using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.Graph;
using NetPrints.Editor.UITests.ClassEditor;
using NetPrints.Testing.Ui.Driving;
using NetPrints.Testing.Ui.Snapshots;

namespace NetPrints.Editor.UITests.Graph;

/// <summary>Hover feedback (pseudo-classes and pixels) and drops onto the canvas.</summary>
public class HoverAndDropTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<bool> PixelsChangeOnHoverAsync(EditorSession session, UiElement element)
    {
        await session.Driver.MoveAsync(await session.Graph.EmptyPointAsync(Token), Token);
        var before = await element.ScreenshotAsync(Token);
        await element.HoverAsync(Token);
        var after = await element.ScreenshotAsync(Token);
        return !SnapshotComparer.Compare(after, before, new SnapshotOptions { PixelThreshold = 4, MaxDiffPercent = 0 }).Matches;
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task HoveringACableHighlightsIt()
    {
        using var session = await EditorSession.OpenSampleMainAsync(Token);
        var cable = session.Graph.Connection("MethodEntryNode.Exec->CallMethodNode.Exec");
        await session.Driver.MoveAsync(await session.Graph.EmptyPointAsync(Token), Token);
        var before = await cable.ScreenshotAroundMidpointAsync(24, Token);

        await cable.HoverAsync(Token);

        var after = await cable.ScreenshotAroundMidpointAsync(24, Token);
        Assert.False(SnapshotComparer.Compare(after, before, new SnapshotOptions { PixelThreshold = 4, MaxDiffPercent = 0 }).Matches,
            "the cable is drawn fully opaque under the pointer (PAR-48)");
        Assert.True((await cable.GetAsync(Token)).Has(":pointerover"));
        Assert.Equal("1", await cable.PropertyAsync("Opacity", Token));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task HoveringAPinHighlightsIt()
    {
        using var session = await EditorSession.OpenSampleMainAsync(Token);
        var pin = session.Graph.Node("CallMethodNode").Input("Exec");

        Assert.True(await PixelsChangeOnHoverAsync(session, pin), "the pin row is highlighted under the pointer");
        Assert.True((await pin.GetAsync(Token)).Has(":pointerover"));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task HoveringANodeHighlightsIt()
    {
        using var session = await EditorSession.OpenSampleMainAsync(Token);
        var node = session.Graph.Node("CallMethodNode");

        await node.Label.HoverAsync(Token);

        Assert.True((await node.GetAsync(Token)).Has(":pointerover"));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task DroppingAMethodOnTheCanvasAddsACallNode()
    {
        using var session = await EditorSession.OpenSampleMainAsync(Token);
        var main = session.ClassVM.Methods.Single(m => m.Name == "Main");
        var data = new DataTransfer();
        data.Add(DataTransferItem.Create(GraphDragDrop.MethodFormat, main)); // what the method list puts on the clipboard
        var at = await session.Graph.EmptyPointAsync(Token);
        var expected = await session.Graph.ToGraphAsync(at, Token);

        session.App.Driver.Drop(at, data); // PAR-56

        await session.WaitForRenderedAsync(Token);
        var call = ((MethodGraph)session.GraphVM.Graph).Nodes.OfType<CallMethodNode>().Single(n => n.MethodSpecifier.Name == "Main");
        Assert.Equal(Math.Max(0, expected.X), call.PositionX, 0);
        Assert.Equal(Math.Max(0, expected.Y), call.PositionY, 0);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task DroppingAVariableOnTheCanvasOffersGetAndSet()
    {
        using var session = await EditorSession.OpenSampleMainAsync(Token);
        await session.ClassEditor.CreateVariableButton.ClickAsync(Token);
        var variable = session.ClassVM.Variables.Single();
        var data = new DataTransfer();
        data.Add(DataTransferItem.Create(GraphDragDrop.VariableFormat, variable));

        session.App.Driver.Drop(await session.Graph.EmptyPointAsync(Token), data); // PAR-57

        await session.Graph.GetSet.WaitOpenAsync(Token);
        await session.Graph.GetSet.SetButton.ClickAsync(Token);
        await session.WaitForRenderedAsync(Token);
        Assert.Single(((MethodGraph)session.GraphVM.Graph).Nodes.OfType<VariableSetterNode>());
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task DroppingSomethingElseIsRefused()
    {
        using var session = await EditorSession.OpenSampleMainAsync(Token);
        var data = new DataTransfer();
        data.Add(DataTransferItem.CreateText("not a method"));
        int nodes = session.GraphVM.Nodes.Count;

        session.App.Driver.Drop(await session.Graph.EmptyPointAsync(Token), data);

        Assert.Equal(nodes, session.GraphVM.Nodes.Count);
    }
}
