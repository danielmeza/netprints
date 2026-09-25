using Avalonia;
using Avalonia.Headless;
using Avalonia.Input;
using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Editor.ViewModels;

namespace NetPrints.Editor.Tests.Ui;

/// <summary>Pointer and keyboard interactions on the real canvas (headless input).</summary>
[TestClass]
public class InteractionTests
{
    [TestMethod]
    [Timeout(90000, CooperativeCancellation = true)]
    public Task DraggingPinToCompatiblePinConnects() => UiTest.RunAsync(async () =>
    {
        using var ctx = await GraphTestContext.OpenSampleMainAsync();
        var method = (MethodGraph)ctx.Graph.Graph;
        var write = ctx.Graph.Nodes.Single(n => n.Node is CallMethodNode);

        // Disconnect entry -> WriteLine, then reconnect it by dragging (PAR-46).
        var entryExec = ctx.Graph.Nodes.Single(n => n.Node == method.EntryNode).OutputExecPins.Single();
        entryExec.DisconnectAll();
        await ctx.WaitForGraphAsync();
        Assert.IsFalse(write.InputExecPins.Single().IsConnected);

        ctx.Window.Drag(ctx.PinPoint(entryExec), ctx.PinPoint(write.InputExecPins.Single()));
        await ctx.WaitForGraphAsync();

        Assert.IsTrue(write.InputExecPins.Single().IsConnected, "dragging from pin to pin connects them");
        Assert.AreSame(write.Node.InputExecPins[0], method.EntryNode.InitialExecutionPin.OutgoingPin);
    });

    [TestMethod]
    [Timeout(90000, CooperativeCancellation = true)]
    public Task DraggingPinToIncompatiblePinDoesNotConnect() => UiTest.RunAsync(async () =>
    {
        using var ctx = await GraphTestContext.OpenSampleMainAsync();
        var method = (MethodGraph)ctx.Graph.Graph;
        var write = ctx.Graph.Nodes.Single(n => n.Node is CallMethodNode);
        var entryExec = ctx.Graph.Nodes.Single(n => n.Node == method.EntryNode).OutputExecPins.Single();
        entryExec.DisconnectAll();
        await ctx.WaitForGraphAsync();
        int connections = ctx.Graph.Connections.Count;

        // Exec output onto a data input: rejected.
        ctx.Window.Drag(ctx.PinPoint(entryExec), ctx.PinPoint(write.InputDataPins.Single()));
        await ctx.WaitForGraphAsync();

        Assert.AreEqual(connections, ctx.Graph.Connections.Count);
        Assert.IsNull(method.EntryNode.InitialExecutionPin.OutgoingPin);
    });

    [TestMethod]
    [Timeout(90000, CooperativeCancellation = true)]
    public Task ReleasingPinDragOnCanvasOpensFilteredSearch() => UiTest.RunAsync(async () =>
    {
        using var ctx = await GraphTestContext.OpenSampleMainAsync();
        var method = (MethodGraph)ctx.Graph.Graph;
        var write = ctx.Graph.Nodes.Single(n => n.Node is CallMethodNode);
        var writeExecOut = write.OutputExecPins.First(p => p.Pin.Name != "Catch");

        ctx.Window.Drag(ctx.PinPoint(writeExecOut), ctx.EmptyCanvasPoint());
        await UiTest.WaitUntilAsync(() => ctx.Graph.Search.IsOpen && !ctx.Graph.Search.IsLoading);

        Assert.AreSame(writeExecOut.Pin, ctx.Graph.Search.SuggestionPin, "the search is filtered for the pin (PAR-47)");
        Assert.IsTrue(ctx.Graph.Search.Items.Any(i => i.Text == "If Else"));
        Assert.IsGreaterThan(0, ctx.Graph.Search.Position.X);

        await ctx.Graph.Search.SelectCommand.ExecuteAsync(ctx.Graph.Search.Items.First(i => i.Text == "If Else"));
        await ctx.WaitForGraphAsync();

        var ifElse = method.Nodes.OfType<IfElseNode>().Single();
        Assert.AreSame(ifElse.InputExecPins[0], ((CallMethodNode)write.Node).OutputExecPins[0].OutgoingPin, "auto-connected");
    });

    [TestMethod]
    [Timeout(90000, CooperativeCancellation = true)]
    public Task RightClickOpensSearchAndTypingFilters() => UiTest.RunAsync(async () =>
    {
        using var ctx = await GraphTestContext.OpenSampleMainAsync();

        ctx.Window.Click(ctx.EmptyCanvasPoint(), MouseButton.Right);
        await UiTest.WaitUntilAsync(() => ctx.Graph.Search.IsOpen && !ctx.Graph.Search.IsLoading);
        Assert.IsNull(ctx.Graph.Search.SuggestionPin);

        var searchView = ctx.View.Find<Views.Graph.NodeSearchView>("SearchView");
        var box = searchView.Find<Avalonia.Controls.TextBox>("SearchBox");
        await UiTest.WaitUntilAsync(() => box.IsFocused, 5000, "the search box is focused on open (PAR-52)");
        Assert.AreEqual("", box.Text ?? "", "the search box is cleared on open");

        ctx.Window.KeyTextInput("write line");
        await UiTest.WaitUntilAsync(() => ctx.Graph.Search.Items.Any(i => i.Value is MethodSpecifier { Name: "WriteLine" })
            && ctx.Graph.Search.Items.All(i => i.IsHeader || i.SearchText.Contains("line", StringComparison.OrdinalIgnoreCase)), 10000);

        Assert.AreEqual(700, searchView.Width, "PAR-52 popup size");
        Assert.AreEqual(300, searchView.Height);
        var list = searchView.Find<Avalonia.Controls.ListBox>("ResultList");
        Assert.IsLessThan(200, list.GetRealizedContainers().Count(), "the result list is virtualized");

        // Enter picks the first suggestion and closes the popup.
        ctx.Window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        await UiTest.WaitUntilAsync(() => !ctx.Graph.Search.IsOpen);
        await ctx.WaitForGraphAsync();
        Assert.AreEqual(4, ctx.Graph.Nodes.Count, "a node was created");
    });

    [TestMethod]
    [Timeout(90000, CooperativeCancellation = true)]
    public Task RightDragPansWithoutOpeningSearch() => UiTest.RunAsync(async () =>
    {
        using var ctx = await GraphTestContext.OpenSampleMainAsync();
        var editor = ctx.View.Find<Nodify.Avalonia.NodifyEditor>("Editor");
        var before = editor.ViewportLocation;

        var start = ctx.EmptyCanvasPoint(-200, -200);
        ctx.Window.Drag(start, new Point(start.X + 100, start.Y + 60), MouseButton.Right);

        Assert.AreNotEqual(before, editor.ViewportLocation, "right drag pans (PAR-51)");
        Assert.IsFalse(ctx.Graph.Search.IsOpen);
    });

    [TestMethod]
    [Timeout(90000, CooperativeCancellation = true)]
    public Task ViewportResetsWhenAnotherGraphOpens() => UiTest.RunAsync(async () =>
    {
        using var ctx = await GraphTestContext.OpenSampleMainAsync();
        var editor = ctx.View.Find<Nodify.Avalonia.NodifyEditor>("Editor");
        var start = ctx.EmptyCanvasPoint(-200, -200);
        ctx.Window.Drag(start, new Point(start.X + 100, start.Y + 60), MouseButton.Right);
        Assert.AreNotEqual(new Point(0, 0), editor.ViewportLocation);

        ctx.Editor.ShowClassCommand.Execute(null);
        UiTest.Pump();

        Assert.AreEqual(new Point(0, 0), editor.ViewportLocation, "PAR-51");
        Assert.AreEqual(1.0, editor.ViewportZoom);
        Assert.AreEqual(0.3, editor.MinViewportZoom);
        Assert.AreEqual(1.0, editor.MaxViewportZoom);
        Assert.AreEqual(28u, (uint)editor.GridCellSize);
    });

    [TestMethod]
    [Timeout(90000, CooperativeCancellation = true)]
    public Task MiddleClickClearsInlineValue() => UiTest.RunAsync(async () =>
    {
        using var ctx = await GraphTestContext.OpenSampleMainAsync();
        var write = ctx.Graph.Nodes.Single(n => n.Node is CallMethodNode);
        var valuePin = write.InputDataPins.Single();
        Assert.AreEqual("Hello, World!", valuePin.UnconnectedText);

        var editorBox = ctx.ConnectorOf(valuePin).Descendants<Avalonia.Controls.TextBox>().First(t => t.IsEffectivelyVisible);
        Assert.AreEqual("Hello, World!", editorBox.Text, "inline editor for the unconnected input (PAR-44)");
        ctx.Window.Click(editorBox.CenterIn(ctx.Window), MouseButton.Middle);

        Assert.IsNull(((NodeInputDataPin)valuePin.Pin).UnconnectedValue, "middle click clears the value");
        Assert.IsFalse(valuePin.IsConnected);
    });

    [TestMethod]
    [Timeout(90000, CooperativeCancellation = true)]
    public Task SelectionClickBoxAndDeselect() => UiTest.RunAsync(async () =>
    {
        using var ctx = await GraphTestContext.OpenSampleMainAsync();
        var nodes = ctx.Graph.Nodes.ToList();
        var write = nodes.Single(n => n.Node is CallMethodNode);

        // Click on a node header selects it (PAR-49).
        var header = ctx.ContainerOf(write).Descendants<Avalonia.Controls.TextBlock>().First(t => t.Name == "NodeLabel");
        ctx.Window.Click(header.CenterIn(ctx.Window));
        Assert.IsTrue(write.IsSelected);
        Assert.HasCount(1, ctx.Graph.SelectedNodes.ToList());

        // Click on empty canvas deselects.
        ctx.Window.Click(ctx.EmptyCanvasPoint());
        Assert.IsFalse(ctx.Graph.SelectedNodes.Any());

        // Left drag on empty canvas box-selects.
        var editor = ctx.View.Find<Nodify.Avalonia.NodifyEditor>("Editor");
        var origin = editor.TranslatePoint(new Point(0, 0), ctx.Window)!.Value;
        ctx.Window.Drag(new Point(origin.X + 5, origin.Y + 5), new Point(origin.X + editor.Bounds.Width - 5, origin.Y + editor.Bounds.Height - 5));
        Assert.HasCount(nodes.Count, ctx.Graph.SelectedNodes.ToList(), "box selection selects all nodes");
    });

    [TestMethod]
    [Timeout(90000, CooperativeCancellation = true)]
    public Task DraggingNodesMovesSelectionAndSnapsToGrid() => UiTest.RunAsync(async () =>
    {
        using var ctx = await GraphTestContext.OpenSampleMainAsync();
        var write = ctx.Graph.Nodes.Single(n => n.Node is CallMethodNode);
        var before = write.Location;
        var header = ctx.ContainerOf(write).Descendants<Avalonia.Controls.TextBlock>().First(t => t.Name == "NodeLabel");
        var start = header.CenterIn(ctx.Window);

        ctx.Window.Drag(start, new Point(start.X + 100, start.Y + 45));

        Assert.AreNotEqual(before, write.Location, "PAR-50");
        Assert.AreEqual(0, write.Location.X % 28, "snapped to the grid");
        Assert.AreEqual(0, write.Location.Y % 28, "snapped to the grid");
        Assert.AreEqual(write.Location.X, write.Node.PositionX, "the model follows");
    });

    [TestMethod]
    [Timeout(90000, CooperativeCancellation = true)]
    public Task DeleteUndoRedoKeys() => UiTest.RunAsync(async () =>
    {
        using var ctx = await GraphTestContext.OpenSampleMainAsync();
        var method = (MethodGraph)ctx.Graph.Graph;

        // Create a variable (undoable) and undo/redo it with Ctrl+Z / Ctrl+Y (PAR-37).
        ctx.Editor.CreateVariableCommand.Execute(null);
        ctx.Window.Focus();
        ctx.Window.KeyPress(Key.Z, RawInputModifiers.Control, PhysicalKey.Z, null);
        Assert.IsEmpty(ctx.Editor.Variables);
        ctx.Window.KeyPress(Key.Y, RawInputModifiers.Control, PhysicalKey.Y, null);
        Assert.HasCount(1, ctx.Editor.Variables);

        // Delete removes selected nodes except entry and main return.
        ctx.Graph.SelectNodes(ctx.Graph.Nodes, deselectPrevious: true);
        ctx.Window.KeyPress(Key.Delete, RawInputModifiers.None, PhysicalKey.Delete, null);
        await ctx.WaitForGraphAsync();

        Assert.IsFalse(method.Nodes.OfType<CallMethodNode>().Any());
        Assert.HasCount(2, method.Nodes);
    });

    [TestMethod]
    [Timeout(90000, CooperativeCancellation = true)]
    public Task CableGestures() => UiTest.RunAsync(async () =>
    {
        using var ctx = await GraphTestContext.OpenSampleMainAsync();
        var method = (MethodGraph)ctx.Graph.Graph;
        var write = ctx.Graph.Nodes.Single(n => n.Node is CallMethodNode);
        var ret = ctx.Graph.Nodes.Single(n => n.Node == method.MainReturnNode);

        // Move the return node level with WriteLine so the cable midpoint is on a straight line.
        ret.Location = new GraphPoint(write.Location.X + 560, write.Location.Y);
        await ctx.WaitForGraphAsync();
        UiTest.Pump();

        var cable = ctx.Graph.Connections.Single(c => c.Target.Node == ret);
        Point Mid() => new((ctx.PinPoint(cable.Source).X + ctx.PinPoint(cable.Target).X) / 2,
            (ctx.PinPoint(cable.Source).Y + ctx.PinPoint(cable.Target).Y) / 2);

        // Mouse back button toggles faint (PAR-48).
        ctx.Window.Click(Mid(), MouseButton.XButton1);
        Assert.IsTrue(cable.IsFaint);
        ctx.Window.Click(Mid(), MouseButton.XButton1);
        Assert.IsFalse(cable.IsFaint);

        // Double click inserts a reroute node midway (wait past the double-click time first, so
        // the back-button clicks above are not counted).
        await Task.Delay(700);
        var mid = Mid();
        ctx.Window.MouseDown(mid, MouseButton.Left);
        ctx.Window.MouseUp(mid, MouseButton.Left);
        ctx.Window.MouseDown(mid, MouseButton.Left);
        ctx.Window.MouseUp(mid, MouseButton.Left);
        await ctx.WaitForGraphAsync();
        Assert.HasCount(1, method.Nodes.OfType<RerouteNode>().ToList());

        // Middle click on a cable disconnects it.
        var reroute = ctx.Graph.Nodes.Single(n => n.IsRerouteNode);
        var toReturn = ctx.Graph.Connections.Single(c => c.Target.Node == ret);
        var point = new Point((ctx.PinPoint(toReturn.Source).X + ctx.PinPoint(toReturn.Target).X) / 2,
            (ctx.PinPoint(toReturn.Source).Y + ctx.PinPoint(toReturn.Target).Y) / 2);
        ctx.Window.Click(point, MouseButton.Middle);
        await ctx.WaitForGraphAsync();
        Assert.IsFalse(ret.InputExecPins.Single().IsConnected);
        _ = reroute;
    });

    [TestMethod]
    [Timeout(90000, CooperativeCancellation = true)]
    public Task MiddleClickOnPinDisconnects() => UiTest.RunAsync(async () =>
    {
        using var ctx = await GraphTestContext.OpenSampleMainAsync();
        var write = ctx.Graph.Nodes.Single(n => n.Node is CallMethodNode);

        ctx.Window.Click(ctx.PinPoint(write.InputExecPins.Single()), MouseButton.Middle);
        await ctx.WaitForGraphAsync();

        Assert.IsFalse(write.InputExecPins.Single().IsConnected, "PAR-48");
    });
}
