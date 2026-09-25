using Avalonia.Headless.XUnit;
using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Editor.Graph;
using NetPrints.Editor.UITests.ClassEditor;
using NetPrints.Testing.Ui.Driving;

namespace NetPrints.Editor.UITests.Graph;

/// <summary>Pointer and keyboard interactions on the real canvas (headless input).</summary>
public class CanvasInteractionTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task DraggingPinToCompatiblePinConnects()
    {
        using var session = await EditorSession.OpenSampleMainAsync(Token);
        var method = (MethodGraph)session.GraphVM.Graph;
        session.GraphVM.Nodes.Single(n => n.Node == method.EntryNode).OutputExecPins.Single().DisconnectAll();
        await session.WaitForRenderedAsync(Token);
        var write = session.Graph.Node("CallMethodNode");

        await session.Graph.Node("MethodEntryNode").Output("Exec").ConnectToAsync(write.Input("Exec"), Token); // PAR-46
        await session.WaitForRenderedAsync(Token);

        Assert.Same(method.Nodes.OfType<CallMethodNode>().Single().InputExecPins[0], method.EntryNode.InitialExecutionPin.OutgoingPin);
        Assert.Contains("MethodEntryNode.Exec->CallMethodNode.Exec", await session.Graph.ConnectionNamesAsync(Token));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task DraggingPinToIncompatiblePinDoesNotConnect()
    {
        using var session = await EditorSession.OpenSampleMainAsync(Token);
        var method = (MethodGraph)session.GraphVM.Graph;
        session.GraphVM.Nodes.Single(n => n.Node == method.EntryNode).OutputExecPins.Single().DisconnectAll();
        await session.WaitForRenderedAsync(Token);
        int connections = session.GraphVM.Connections.Count;
        var write = session.Graph.Node("CallMethodNode");
        string dataPin = session.GraphVM.Nodes.Single(n => n.Node is CallMethodNode).InputDataPins.Single().Pin.Name;

        await session.Graph.Node("MethodEntryNode").Output("Exec").ConnectToAsync(write.Input(dataPin), Token); // exec onto data
        await session.WaitForRenderedAsync(Token);

        Assert.Equal(connections, session.GraphVM.Connections.Count);
        Assert.Null(method.EntryNode.InitialExecutionPin.OutgoingPin);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task RightDragPansWithoutOpeningSearch()
    {
        using var session = await EditorSession.OpenSampleMainAsync(Token);
        var before = await session.Graph.ViewportAsync(Token);

        await session.Graph.RightDragAsync(100, 60, Token); // PAR-51

        Assert.NotEqual(before, await session.Graph.ViewportAsync(Token));
        Assert.False(await session.Graph.Search.IsOpenAsync(Token));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task PanningShowsTheMoveCursorUntilReleased()
    {
        using var session = await EditorSession.OpenSampleMainAsync(Token);
        Assert.Null(await session.Graph.CursorAsync(Token));

        var at = await session.Graph.BeginRightDragAsync(100, 60, Token); // PAR-51
        Assert.Equal("SizeAll", await session.Graph.CursorAsync(Token));

        await session.Driver.ReleaseAsync(at, UiButton.Right, Token);
        Assert.Null(await session.Graph.CursorAsync(Token));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task RightClickWithoutMovingKeepsTheDefaultCursor()
    {
        using var session = await EditorSession.OpenSampleMainAsync(Token);

        var search = await session.Graph.RightClickEmptyAsync(Token);
        await search.WaitOpenAsync(Token);

        Assert.Null(await session.Graph.CursorAsync(Token));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task WheelZoomsAroundPointerWithinLimits()
    {
        using var session = await EditorSession.OpenSampleMainAsync(Token);
        var graph = session.Graph;
        var at = await graph.EmptyPointAsync(Token, -300, -300);
        var anchor = await graph.ToGraphAsync(at, Token);

        await graph.WheelAsync(at, -1, Token); // PAR-51: zoom out
        double zoom = await graph.ZoomAsync(Token);
        Assert.True(zoom < 1.0, $"zoomed out to {zoom}");
        var after = await graph.ToGraphAsync(at, Token);
        Assert.True(Math.Abs(after.X - anchor.X) < 1 && Math.Abs(after.Y - anchor.Y) < 1,
            $"the point under the pointer stays put: {anchor} -> {after}");

        for (int i = 0; i < 30; i++)
        {
            await graph.WheelAsync(at, -1, Token);
        }

        Assert.Equal(0.3, await graph.ZoomAsync(Token), 3); // clamped at the minimum

        for (int i = 0; i < 30; i++)
        {
            await graph.WheelAsync(at, 1, Token);
        }

        Assert.Equal(1.0, await graph.ZoomAsync(Token), 3); // clamped at the maximum
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task ViewportResetsWhenAnotherGraphOpens()
    {
        using var session = await EditorSession.OpenSampleMainAsync(Token);
        var graph = session.Graph;
        await graph.RightDragAsync(100, 60, Token);
        await graph.WheelAsync(await graph.EmptyPointAsync(Token, -300, -300), -1, Token);

        await session.ClassEditor.ClassButton.ClickAsync(Token);

        Assert.Equal((0.0, 0.0), await graph.ViewportAsync(Token)); // PAR-51
        Assert.Equal(1.0, await graph.ZoomAsync(Token));
        Assert.Equal(28, await graph.GridCellSizeAsync(Token)); // PAR-38
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task MiddleClickClearsInlineValue()
    {
        using var session = await EditorSession.OpenSampleMainAsync(Token);
        var valuePin = session.GraphVM.Nodes.Single(n => n.Node is CallMethodNode).InputDataPins.Single();
        var valueBox = session.Graph.Node("CallMethodNode").Input(valuePin.Pin.Name).ValueBox;
        Assert.Equal("Hello, World!", await valueBox.TextAsync(Token)); // PAR-44

        await valueBox.ClickAsync(UiButton.Middle, Token);

        Assert.Null(((NodeInputDataPin)valuePin.Pin).UnconnectedValue);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task SelectionClickBoxAndDeselect()
    {
        using var session = await EditorSession.OpenSampleMainAsync(Token);
        var write = session.Graph.Node("CallMethodNode");

        await write.SelectAsync(Token); // PAR-49
        Assert.True(await write.IsSelectedAsync(Token));
        Assert.Equal([session.GraphVM.Nodes.Single(n => n.Node is CallMethodNode)], session.GraphVM.SelectedNodes);

        await session.Graph.ClickEmptyAsync(Token);
        Assert.False(await write.IsSelectedAsync(Token));
        Assert.Empty(session.GraphVM.SelectedNodes);

        await session.Graph.BoxSelectAllAsync(Token);
        Assert.Equal(session.GraphVM.Nodes.Count, session.GraphVM.SelectedNodes.Count());
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task DraggingSelectedNodesMovesThemOnTheGrid()
    {
        using var session = await EditorSession.OpenSampleMainAsync(Token);
        var write = session.GraphVM.Nodes.Single(n => n.Node is CallMethodNode);
        var entry = session.GraphVM.Nodes.Single(n => n.Node is MethodEntryNode);
        var (writeBefore, entryBefore) = (write.Location, entry.Location);
        session.GraphVM.SelectNodes([write, entry], deselectPrevious: true);

        await session.Graph.Node("CallMethodNode").MoveByAsync(100, 45, Token); // PAR-50

        Assert.NotEqual(writeBefore, write.Location);
        Assert.Equal(write.Location.X - writeBefore.X, entry.Location.X - entryBefore.X); // all selected nodes move
        Assert.Equal(0, write.Location.X % 28);
        Assert.Equal(0, write.Location.Y % 28);
        Assert.Equal(write.Location.X, write.Node.PositionX);
        Assert.Equal((write.Location.X, write.Location.Y), await session.Graph.Node("CallMethodNode").LocationAsync(Token));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task CableGestures()
    {
        using var session = await EditorSession.OpenSampleMainAsync(Token);
        var method = (MethodGraph)session.GraphVM.Graph;
        var write = session.GraphVM.Nodes.Single(n => n.Node is CallMethodNode);
        var ret = session.GraphVM.Nodes.Single(n => n.Node == method.MainReturnNode);
        ret.Location = new GraphPoint(write.Location.X + 560, write.Location.Y); // a straight cable
        await session.WaitForRenderedAsync(Token);
        string toReturnName = session.GraphVM.Connections.Single(c => c.Target.Node == ret).AutomationName;

        // Double click inserts a reroute node midway (PAR-48).
        await session.Graph.Connection(toReturnName).DoubleClickAsync(Token);
        await session.WaitForRenderedAsync(Token);
        Assert.Single(method.Nodes.OfType<RerouteNode>());

        // The mouse back button toggles "faint".
        var toReturn = session.GraphVM.Connections.Single(c => c.Target.Node == ret);
        var cable = session.Graph.Connection(toReturn.AutomationName);
        await cable.ClickAsync(UiButton.Back, Token);
        Assert.True(toReturn.IsFaint);
        Assert.True((await cable.GetAsync(Token)).Has("faint"));
        await cable.ClickAsync(UiButton.Back, Token);
        Assert.False(toReturn.IsFaint);

        // Middle click disconnects.
        await cable.ClickAsync(UiButton.Middle, Token);
        await session.WaitForRenderedAsync(Token);
        Assert.False(ret.InputExecPins.Single().IsConnected);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task MiddleClickOnPinDisconnects()
    {
        using var session = await EditorSession.OpenSampleMainAsync(Token);
        var write = session.GraphVM.Nodes.Single(n => n.Node is CallMethodNode);

        await session.Graph.Node("CallMethodNode").Input("Exec").DisconnectAsync(Token); // PAR-48
        await session.WaitForRenderedAsync(Token);

        Assert.False(write.InputExecPins.Single().IsConnected);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task NodeChromeOverloadsPureAndPinButtons()
    {
        using var session = await EditorSession.OpenSampleMainAsync(Token);
        var method = (MethodGraph)session.GraphVM.Graph;
        var write = session.GraphVM.Nodes.Single(n => n.Node is CallMethodNode);
        var node = session.Graph.Node("CallMethodNode");

        Assert.True(await node.Overloads.IsVisibleAsync(Token)); // PAR-40
        var intOverload = write.Overloads.OfType<MethodSpecifier>().First(m => m.Parameters.Count == 1 && m.Parameters[0].Value == TypeSpecifier.FromType<int>());
        write.SelectedOverload = intOverload; // the overload list is a combo box; choosing an item is its own contract
        await session.WaitForRenderedAsync(Token);
        Assert.Equal(intOverload, method.Nodes.OfType<CallMethodNode>().Single().MethodSpecifier);

        await session.ClassEditor.PressUndoAsync(Token);
        await session.WaitForRenderedAsync(Token);
        Assert.Equal(TypeSpecifier.FromType<string>(), method.Nodes.OfType<CallMethodNode>().Single().MethodSpecifier.Parameters[0].Value);

        await session.Graph.Node("MethodEntryNode").LeftPlus.ClickAsync(Token); // PAR-42
        Assert.Single(method.ArgumentTypes);

        await session.Graph.Node("CallMethodNode").Pure.ClickAsync(Token); // PAR-41
        await session.WaitForRenderedAsync(Token);
        Assert.True(method.Nodes.OfType<CallMethodNode>().Single().IsPure);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task GetSetPopupCreatesNodes()
    {
        using var session = await EditorSession.OpenSampleMainAsync(Token);
        var method = (MethodGraph)session.GraphVM.Graph;
        var length = new VariableSpecifier("Length", TypeSpecifier.FromType<int>(), MemberVisibility.Public, MemberVisibility.Private,
            TypeSpecifier.FromType<string>(), VariableModifiers.None);
        var chooser = session.Graph.GetSet;

        session.GraphVM.GetSetChooser.Open(length, new GraphPoint(56, 400)); // PAR-55
        await chooser.WaitOpenAsync(Token);
        Assert.True(await chooser.GetButton.IsEnabledAsync(Token));
        Assert.False(await chooser.SetButton.IsEnabledAsync(Token)); // a private setter of another type

        await chooser.GetButton.ClickAsync(Token);
        await session.WaitForRenderedAsync(Token);
        Assert.Single(method.Nodes.OfType<VariableGetterNode>());
        await chooser.WaitClosedAsync(Token);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task GetSetPopupClosesWhenThePointerLeavesIt()
    {
        using var session = await EditorSession.OpenSampleMainAsync(Token);
        var length = new VariableSpecifier("Length", TypeSpecifier.FromType<int>(), MemberVisibility.Public, MemberVisibility.Public,
            TypeSpecifier.FromType<string>(), VariableModifiers.None);
        var chooser = session.Graph.GetSet;
        session.GraphVM.GetSetChooser.Open(length, new GraphPoint(56, 400));
        await chooser.WaitOpenAsync(Token);

        await chooser.View.HoverAsync(Token); // PAR-55: enter, then leave
        Assert.True(await chooser.IsOpenAsync(Token));
        await session.Driver.MoveAsync(await session.Graph.EmptyPointAsync(Token), Token);

        await chooser.WaitClosedAsync(Token);
        Assert.False(session.GraphVM.GetSetChooser.IsOpen);
    }
}
