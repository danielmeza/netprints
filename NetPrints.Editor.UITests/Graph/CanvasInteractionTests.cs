using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Editor.UITests.ClassEditor;

namespace NetPrints.Editor.UITests.Graph;

/// <summary>Pointer and keyboard interactions on the real canvas (headless input).</summary>
public class CanvasInteractionTests
{
    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task DraggingPinToCompatiblePinConnects()
    {
        using var session = await EditorSession.OpenSampleMainAsync();
        var graph = session.Graph;
        var method = (MethodGraph)graph.ViewModel.Graph;
        var write = graph.ViewModel.Nodes.Single(n => n.Node is CallMethodNode);
        var entryExec = graph.ViewModel.Nodes.Single(n => n.Node == method.EntryNode).OutputExecPins.Single();
        entryExec.DisconnectAll();
        await graph.WaitForRenderedAsync();

        graph.DragPin(entryExec, write.InputExecPins.Single()); // PAR-46
        await graph.WaitForRenderedAsync();

        Assert.Same(write.Node.InputExecPins[0], method.EntryNode.InitialExecutionPin.OutgoingPin);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task DraggingPinToIncompatiblePinDoesNotConnect()
    {
        using var session = await EditorSession.OpenSampleMainAsync();
        var graph = session.Graph;
        var method = (MethodGraph)graph.ViewModel.Graph;
        var write = graph.ViewModel.Nodes.Single(n => n.Node is CallMethodNode);
        var entryExec = graph.ViewModel.Nodes.Single(n => n.Node == method.EntryNode).OutputExecPins.Single();
        entryExec.DisconnectAll();
        await graph.WaitForRenderedAsync();
        int connections = graph.ViewModel.Connections.Count;

        graph.DragPin(entryExec, write.InputDataPins.Single()); // exec output onto a data input
        await graph.WaitForRenderedAsync();

        Assert.Equal(connections, graph.ViewModel.Connections.Count);
        Assert.Null(method.EntryNode.InitialExecutionPin.OutgoingPin);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task RightDragPansWithoutOpeningSearch()
    {
        using var session = await EditorSession.OpenSampleMainAsync();
        var before = session.Graph.Editor.ViewportLocation;

        session.Graph.RightDrag(new Vector(100, 60)); // PAR-51

        Assert.NotEqual(before, session.Graph.Editor.ViewportLocation);
        Assert.False(session.Graph.ViewModel.Search.IsOpen);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task WheelZoomsAroundPointerWithinLimits()
    {
        using var session = await EditorSession.OpenSampleMainAsync();
        var graph = session.Graph;
        var at = graph.EmptyPoint(-300, -300);
        var anchor = graph.ToGraph(at);

        graph.Zoom(at, -1); // PAR-51: zoom out
        Assert.True(graph.Editor.ViewportZoom < 1.0, $"zoomed out to {graph.Editor.ViewportZoom}");
        var after = graph.ToGraph(at);
        Assert.True(Math.Abs(after.X - anchor.X) < 1 && Math.Abs(after.Y - anchor.Y) < 1,
            $"the point under the pointer stays put: {anchor} -> {after}");

        for (int i = 0; i < 30; i++)
        {
            graph.Zoom(at, -1);
        }

        Assert.Equal(0.3, graph.Editor.ViewportZoom, 3); // clamped at the minimum

        for (int i = 0; i < 30; i++)
        {
            graph.Zoom(at, 1);
        }

        Assert.Equal(1.0, graph.Editor.ViewportZoom, 3); // clamped at the maximum
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task ViewportResetsWhenAnotherGraphOpens()
    {
        using var session = await EditorSession.OpenSampleMainAsync();
        var graph = session.Graph;
        graph.RightDrag(new Vector(100, 60));
        graph.Zoom(graph.EmptyPoint(-300, -300), -1);

        session.ClassEditor.ClickClassButton();

        Assert.Equal(new Point(0, 0), graph.Editor.ViewportLocation); // PAR-51
        Assert.Equal(1.0, graph.Editor.ViewportZoom);
        Assert.Equal(28u, (uint)graph.Editor.GridCellSize); // PAR-38
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task MiddleClickClearsInlineValue()
    {
        using var session = await EditorSession.OpenSampleMainAsync();
        var graph = session.Graph;
        var write = graph.ViewModel.Nodes.Single(n => n.Node is CallMethodNode);
        var valuePin = write.InputDataPins.Single();
        var editor = graph.ConnectorOf(valuePin).ById<TextBox>(AutomationIds.PinValueText);
        Assert.Equal("Hello, World!", editor.Text); // PAR-44

        session.ClassEditor.Window.Click(editor.CenterIn(session.ClassEditor.Window), MouseButton.Middle);

        Assert.Null(((NodeInputDataPin)valuePin.Pin).UnconnectedValue);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task SelectionClickBoxAndDeselect()
    {
        using var session = await EditorSession.OpenSampleMainAsync();
        var graph = session.Graph;
        var write = graph.ViewModel.Nodes.Single(n => n.Node is CallMethodNode);

        graph.ClickNode(write); // PAR-49
        Assert.Equal([write], graph.ViewModel.SelectedNodes);

        graph.ClickEmpty();
        Assert.Empty(graph.ViewModel.SelectedNodes);

        graph.BoxSelectAll();
        Assert.Equal(graph.ViewModel.Nodes.Count, graph.ViewModel.SelectedNodes.Count());
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task DraggingSelectedNodesMovesThemOnTheGrid()
    {
        using var session = await EditorSession.OpenSampleMainAsync();
        var graph = session.Graph;
        var write = graph.ViewModel.Nodes.Single(n => n.Node is CallMethodNode);
        var entry = graph.ViewModel.Nodes.Single(n => n.Node is MethodEntryNode);
        var (writeBefore, entryBefore) = (write.Location, entry.Location);
        graph.ViewModel.SelectNodes([write, entry], deselectPrevious: true);

        graph.DragNode(write, new Vector(100, 45)); // PAR-50

        Assert.NotEqual(writeBefore, write.Location);
        Assert.Equal(write.Location.X - writeBefore.X, entry.Location.X - entryBefore.X); // all selected nodes move
        Assert.Equal(0, write.Location.X % 28);
        Assert.Equal(0, write.Location.Y % 28);
        Assert.Equal(write.Location.X, write.Node.PositionX);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task CableGestures()
    {
        using var session = await EditorSession.OpenSampleMainAsync();
        var graph = session.Graph;
        var window = session.ClassEditor.Window;
        var method = (MethodGraph)graph.ViewModel.Graph;
        var write = graph.ViewModel.Nodes.Single(n => n.Node is CallMethodNode);
        var ret = graph.ViewModel.Nodes.Single(n => n.Node == method.MainReturnNode);
        ret.Location = new NetPrints.Editor.Graph.GraphPoint(write.Location.X + 560, write.Location.Y); // a straight cable
        await graph.WaitForRenderedAsync();

        // Double click inserts a reroute node midway (PAR-48).
        window.DoubleClick(graph.CablePoint(graph.ViewModel.Connections.Single(c => c.Target.Node == ret)));
        await graph.WaitForRenderedAsync();
        Assert.Single(method.Nodes.OfType<RerouteNode>());

        // The mouse back button toggles "faint".
        var toReturn = graph.ViewModel.Connections.Single(c => c.Target.Node == ret);
        window.Click(graph.CablePoint(toReturn), MouseButton.XButton1);
        Assert.True(toReturn.IsFaint);
        window.Click(graph.CablePoint(toReturn), MouseButton.XButton1);
        Assert.False(toReturn.IsFaint);

        // Middle click disconnects.
        window.Click(graph.CablePoint(toReturn), MouseButton.Middle);
        await graph.WaitForRenderedAsync();
        Assert.False(ret.InputExecPins.Single().IsConnected);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task MiddleClickOnPinDisconnects()
    {
        using var session = await EditorSession.OpenSampleMainAsync();
        var write = session.Graph.ViewModel.Nodes.Single(n => n.Node is CallMethodNode);

        session.ClassEditor.Window.Click(session.Graph.PinPoint(write.InputExecPins.Single()), MouseButton.Middle); // PAR-48
        await session.Graph.WaitForRenderedAsync();

        Assert.False(write.InputExecPins.Single().IsConnected);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task NodeChromeOverloadsPureAndPinButtons()
    {
        using var session = await EditorSession.OpenSampleMainAsync();
        var graph = session.Graph;
        var method = (MethodGraph)graph.ViewModel.Graph;
        var write = graph.ViewModel.Nodes.Single(n => n.Node is CallMethodNode);

        var overloads = graph.NodeControl<ComboBox>(write, AutomationIds.NodeOverloads); // PAR-40
        Assert.True(overloads.IsVisible);
        var intOverload = write.Overloads.OfType<MethodSpecifier>().First(m => m.Parameters.Count == 1 && m.Parameters[0].Value == TypeSpecifier.FromType<int>());
        overloads.SelectedItem = intOverload;
        await graph.WaitForRenderedAsync();
        Assert.Equal(intOverload, method.Nodes.OfType<CallMethodNode>().Single().MethodSpecifier);

        session.ClassEditor.PressUndo();
        await graph.WaitForRenderedAsync();
        Assert.Equal(TypeSpecifier.FromType<string>(), method.Nodes.OfType<CallMethodNode>().Single().MethodSpecifier.Parameters[0].Value);

        var entry = graph.ViewModel.Nodes.Single(n => n.Node == method.EntryNode); // PAR-42
        session.ClassEditor.Window.Click(graph.NodeControl<Button>(entry, AutomationIds.NodeLeftPlus).CenterIn(session.ClassEditor.Window));
        Assert.Single(method.ArgumentTypes);

        var call = graph.ViewModel.Nodes.Single(n => n.Node is CallMethodNode); // PAR-41
        graph.NodeControl<CheckBox>(call, AutomationIds.NodePure).IsChecked = true;
        await graph.WaitForRenderedAsync();
        Assert.True(method.Nodes.OfType<CallMethodNode>().Single().IsPure);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task GetSetPopupCreatesNodes()
    {
        using var session = await EditorSession.OpenSampleMainAsync();
        var graph = session.Graph;
        var method = (MethodGraph)graph.ViewModel.Graph;
        var length = new VariableSpecifier("Length", TypeSpecifier.FromType<int>(), MemberVisibility.Public, MemberVisibility.Private,
            TypeSpecifier.FromType<string>(), VariableModifiers.None);

        graph.ViewModel.GetSetChooser.Open(length, new NetPrints.Editor.Graph.GraphPoint(56, 400)); // PAR-55
        await HeadlessInput.WaitUntilAsync(() => graph.IsGetSetOpen, "Get/Set popup open");
        Assert.True(graph.GetButton.IsEffectivelyEnabled);
        Assert.False(graph.SetButton.IsEffectivelyEnabled); // a private setter of another type

        graph.GetButton.Command!.Execute(null);
        await graph.WaitForRenderedAsync();
        Assert.Single(method.Nodes.OfType<VariableGetterNode>());
        Assert.False(graph.ViewModel.GetSetChooser.IsOpen);
    }
}
