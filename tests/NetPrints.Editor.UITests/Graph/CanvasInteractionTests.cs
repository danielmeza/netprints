using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using NetPrints.Core;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Editor.UITests.Driving;
using NetPrints.Editor.UITests.Shell;
using NetPrints.Graph;
using NetPrints.Testing.Ui.Driving;
using Nodify.Avalonia.Connections;

namespace NetPrints.Editor.UITests.Graph;

/// <summary>Pointer and keyboard interactions on the real canvas (headless input).</summary>
public class CanvasInteractionTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task CtrlEnterOnAFocusedConnectorStartsAConnection()
    {
        // REVIEW-NOTE (PR #6): Ctrl+Enter replaces bare Space as the connector's keyboard "Connect"
        // gesture (GraphEditorGestures), so a keyboard-only path to connect a pin still exists once
        // Space/Delete are freed for typing (OWN-06). Focusing the Connector directly (rather than
        // clicking it, which would itself fire the mouse "Connect" gesture) isolates the keyboard path.
        // Nodify's Connect gesture is a drag: the key's own release, like a mouse-up, ends the pending
        // connection (there is no other connector at the same point to land on), so the press and the
        // release are sent separately, checking the pending state in between instead of through the
        // driver's single combined PressAsync.
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var pin = session.GraphViewModel.Nodes.Single(n => n.Node is CallMethodNode).OutputExecPins.First(p => p.Pin.Name != "Catch");
        var connector = session.Window.GetVisualDescendants().OfType<Connector>().Single(c => ReferenceEquals(c.DataContext, pin));

        connector.Focus();
        HeadlessDriver.Pump();
        Assert.False(connector.IsPendingConnection);

        session.Window.KeyPress(Key.Enter, RawInputModifiers.Control, PhysicalKey.None, null);
        HeadlessDriver.Pump();

        Assert.True(connector.IsPendingConnection);

        session.Window.KeyRelease(Key.Enter, RawInputModifiers.Control, PhysicalKey.None, null);
        HeadlessDriver.Pump();
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task CtrlSpaceInAPinValueTextBoxDoesNotOpenSearch()
    {
        // R2-15: the canvas's scoped key behavior must leave a text box alone instead of opening
        // the node search over whatever the user is typing.
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var valuePin = session.GraphViewModel.Nodes.Single(n => n.Node is CallMethodNode).InputDataPins.Single();
        var valueBox = session.Graph.Node("CallMethodNode").Input(valuePin.Pin.Name).ValueBox;

        await valueBox.ClickAsync(Token);
        await session.Driver.PressAsync("Ctrl+Space", Token);

        Assert.Equal("True", await valueBox.PropertyAsync(AutomationPropertyNames.IsFocused, Token));
        Assert.False(await session.Graph.Search.IsOpenAsync(Token));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task DraggingPinToCompatiblePinConnects()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var method = (MethodGraph)session.GraphViewModel.Graph;
        session.GraphViewModel.Nodes.Single(n => n.Node == method.EntryNode).OutputExecPins.Single().DisconnectAll();
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
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var method = (MethodGraph)session.GraphViewModel.Graph;
        session.GraphViewModel.Nodes.Single(n => n.Node == method.EntryNode).OutputExecPins.Single().DisconnectAll();
        await session.WaitForRenderedAsync(Token);
        int connections = session.GraphViewModel.Connections.Count;
        var write = session.Graph.Node("CallMethodNode");
        string dataPin = session.GraphViewModel.Nodes.Single(n => n.Node is CallMethodNode).InputDataPins.Single().Pin.Name;

        await session.Graph.Node("MethodEntryNode").Output("Exec").ConnectToAsync(write.Input(dataPin), Token); // exec onto data
        await session.WaitForRenderedAsync(Token);

        Assert.Equal(connections, session.GraphViewModel.Connections.Count);
        Assert.Null(method.EntryNode.InitialExecutionPin.OutgoingPin);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task RightDragPansWithoutOpeningSearch()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var before = await session.Graph.ViewportAsync(Token);

        await session.Graph.RightDragAsync(100, 60, Token); // PAR-51

        Assert.NotEqual(before, await session.Graph.ViewportAsync(Token));
        Assert.False(await session.Graph.Search.IsOpenAsync(Token));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task PanningShowsTheMoveCursorUntilReleased()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        Assert.Null(await session.Graph.CursorAsync(Token));

        var at = await session.Graph.BeginRightDragAsync(100, 60, Token); // PAR-51
        Assert.Equal("SizeAll", await session.Graph.CursorAsync(Token));

        await session.Driver.ReleaseAsync(at, UiButton.Right, Token);
        Assert.Null(await session.Graph.CursorAsync(Token));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task RightClickWithoutMovingKeepsTheDefaultCursor()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);

        var search = await session.Graph.RightClickEmptyAsync(Token);
        await search.WaitOpenAsync(Token);

        Assert.Null(await session.Graph.CursorAsync(Token));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task WheelZoomsAroundPointerWithinLimits()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
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
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var graph = session.Graph;
        await graph.RightDragAsync(100, 60, Token);
        await graph.WheelAsync(await graph.EmptyPointAsync(Token, -300, -300), -1, Token);

        await session.OpenClassGraphAsync(Token);

        Assert.Equal((0.0, 0.0), await graph.ViewportAsync(Token)); // PAR-51
        Assert.Equal(1.0, await graph.ZoomAsync(Token));
        Assert.Equal(28, await graph.GridCellSizeAsync(Token)); // PAR-38
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task MiddleClickClearsInlineValue()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var valuePin = session.GraphViewModel.Nodes.Single(n => n.Node is CallMethodNode).InputDataPins.Single();
        var valueBox = session.Graph.Node("CallMethodNode").Input(valuePin.Pin.Name).ValueBox;
        Assert.Equal("Hello, World!", await valueBox.TextAsync(Token)); // PAR-44

        await valueBox.ClickAsync(UiButton.Middle, Token);

        Assert.Null(((NodeInputDataPin)valuePin.Pin).UnconnectedValue);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task SelectionClickBoxAndDeselect()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var write = session.Graph.Node("CallMethodNode");

        await write.SelectAsync(Token); // PAR-49
        Assert.True(await write.IsSelectedAsync(Token));
        Assert.Equal([session.GraphViewModel.Nodes.Single(n => n.Node is CallMethodNode)], session.GraphViewModel.SelectedNodes);

        await session.Graph.ClickEmptyAsync(Token);
        Assert.False(await write.IsSelectedAsync(Token));
        Assert.Empty(session.GraphViewModel.SelectedNodes);

        await session.Graph.BoxSelectAllAsync(Token);
        Assert.Equal(session.GraphViewModel.Nodes.Count, session.GraphViewModel.SelectedNodes.Count());
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task DraggingSelectedNodesMovesThemOnTheGrid()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var write = session.GraphViewModel.Nodes.Single(n => n.Node is CallMethodNode);
        var entry = session.GraphViewModel.Nodes.Single(n => n.Node is MethodEntryNode);
        var (writeBefore, entryBefore) = (write.Location, entry.Location);
        session.GraphViewModel.SelectNodes([write, entry], deselectPrevious: true);

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
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var method = (MethodGraph)session.GraphViewModel.Graph;
        var write = session.GraphViewModel.Nodes.Single(n => n.Node is CallMethodNode);
        var ret = session.GraphViewModel.Nodes.Single(n => n.Node == method.MainReturnNode);
        ret.Location = new GraphPoint(write.Location.X + 560, write.Location.Y); // a straight cable
        await session.WaitForRenderedAsync(Token);
        string toReturnName = session.GraphViewModel.Connections.Single(c => c.Target.Node == ret).AutomationName;

        // Double click inserts a reroute node midway (PAR-48).
        await session.Graph.Connection(toReturnName).DoubleClickAsync(Token);
        await session.WaitForRenderedAsync(Token);
        Assert.Single(method.Nodes.OfType<RerouteNode>());

        // The mouse back button toggles "faint".
        var toReturn = session.GraphViewModel.Connections.Single(c => c.Target.Node == ret);
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
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var write = session.GraphViewModel.Nodes.Single(n => n.Node is CallMethodNode);

        await session.Graph.Node("CallMethodNode").Input("Exec").DisconnectAsync(Token); // PAR-48
        await session.WaitForRenderedAsync(Token);

        Assert.False(write.InputExecPins.Single().IsConnected);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task NodeChromeOverloadsPureAndPinButtons()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var method = (MethodGraph)session.GraphViewModel.Graph;
        var write = session.GraphViewModel.Nodes.Single(n => n.Node is CallMethodNode);
        var node = session.Graph.Node("CallMethodNode");

        Assert.True(await node.Overloads.IsVisibleAsync(Token)); // PAR-40
        var intOverload = write.Overloads.OfType<MethodSpecifier>().First(m => m.Parameters.Count == 1 && m.Parameters[0].Value == TypeSpecifier.FromType<int>());
        write.SelectedOverload = intOverload; // the overload list is a combo box; choosing an item is its own contract
        await session.WaitForRenderedAsync(Token);
        Assert.Equal(intOverload, method.Nodes.OfType<CallMethodNode>().Single().MethodSpecifier);

        await session.PressUndoAsync(Token);
        await session.WaitForRenderedAsync(Token);
        Assert.Equal(TypeSpecifier.FromType<string>(), method.Nodes.OfType<CallMethodNode>().Single().MethodSpecifier.Parameters[0].Value);

        await session.Graph.Node("MethodEntryNode").LeftPlus.ClickAsync(Token); // PAR-42
        Assert.Single(method.ArgumentTypes);

        Assert.False(await node.Pure.IsVisibleAsync(Token)); // a void call cannot be pure

        var stringType = TypeSpecifier.FromType<string>();
        var toUpper = session.GraphViewModel.AddNode<CallMethodNode>(new GraphPoint(280, 392), null,
            new MethodSpecifier("ToUpper", [], [stringType], MethodModifiers.None, MemberVisibility.Public, stringType, []));
        await session.WaitForRenderedAsync(Token);
        await session.Graph.Node(toUpper.Name).Pure.ClickAsync(Token); // PAR-41
        await session.WaitForRenderedAsync(Token);
        Assert.True(toUpper.IsPure);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task GetSetPopupCreatesNodes()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var method = (MethodGraph)session.GraphViewModel.Graph;
        var length = new VariableSpecifier("Length", TypeSpecifier.FromType<int>(), MemberVisibility.Public, MemberVisibility.Private,
            TypeSpecifier.FromType<string>(), VariableModifiers.None);
        var chooser = session.Graph.GetSet;

        session.GraphViewModel.GetSetChooser.Open(length, new GraphPoint(56, 400)); // PAR-55
        await chooser.WaitOpenAsync(Token);
        Assert.True(await chooser.GetButton.IsEnabledAsync(Token));
        Assert.False(await chooser.SetButton.IsEnabledAsync(Token)); // a private setter of another type

        await chooser.GetButton.ClickAsync(Token);
        await session.WaitForRenderedAsync(Token);
        Assert.Single(method.Nodes.OfType<VariableGetterNode>());
        await chooser.WaitClosedAsync(Token);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task GetSetPopupClosesOnAnOutsideClick()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var length = new VariableSpecifier("Length", TypeSpecifier.FromType<int>(), MemberVisibility.Public, MemberVisibility.Public,
            TypeSpecifier.FromType<string>(), VariableModifiers.None);
        var chooser = session.Graph.GetSet;
        session.GraphViewModel.GetSetChooser.Open(length, new GraphPoint(56, 400));
        await chooser.WaitOpenAsync(Token);

        await chooser.View.HoverAsync(Token);
        Assert.True(await chooser.IsOpenAsync(Token));

        // ADR-0004: CanvasPopup light-dismisses on a click outside it, not on the pointer merely leaving it.
        await session.Driver.ClickAsync(await session.Graph.EmptyPointAsync(Token), UiButton.Left, 1, Token);

        await chooser.WaitClosedAsync(Token);
        Assert.False(session.GraphViewModel.GetSetChooser.IsOpen);
    }
}
