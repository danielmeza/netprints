using NetPrints.Editor.Commands;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Editor.Tests.Shell;
using NetPrints.Graph;

namespace NetPrints.Editor.Tests.Graph;

/// <summary>Adding a node from the palette and deleting nodes from the canvas or the Edit menu go through the class's undo stack (FR-035).</summary>
public class NodeUndoTests : GraphTestBase
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public NodeUndoTests(TestEditor editor)
        : base(editor)
    {
        ClassContext.UndoRedo.MarkSaved();
        Class.MarkClean();
    }

    private async Task<IfElseNode> AddIfElseAsync(GraphPoint position, NodePin? pin = null)
    {
        await Graph.OpenSearchAsync(position, pin, Token);
        await Graph.Search.SelectCommand.ExecuteAsync(Graph.Search.Items.First(item => item.Text == "If Else"));
        return Method.Nodes.OfType<IfElseNode>().Last();
    }

    private Task DeleteAsync(params Node[] nodes)
    {
        Graph.SelectNodes(nodes.Select(VmOf), deselectPrevious: true);
        var context = new CommandContext(new FakeShell(), null, null, Graph, new CommandSelection([.. Graph.SelectedNodes]));
        return new DeleteCommandHandler().ExecuteAsync(context, Token);
    }

    [Fact(Timeout = 60000)]
    public async Task ANodeAddedFromThePaletteIsUndoneAndRedone()
    {
        IfElseNode node = await AddIfElseAsync(new GraphPoint(140, 84));

        Assert.Equal("Add node", ClassContext.UndoRedo.UndoName);

        ClassContext.UndoRedo.Undo();
        Assert.DoesNotContain(node, Method.Nodes);

        ClassContext.UndoRedo.Redo();
        Assert.Contains(node, Method.Nodes);
        Assert.Equal((140, 84), (node.PositionX, node.PositionY));
        Assert.NotNull(Graph.Nodes.FirstOrDefault(vm => vm.Node == node));
    }

    [Fact(Timeout = 60000)]
    public async Task UndoingAPinSearchAddDisconnectsTheNodeAndRedoConnectsItAgain()
    {
        NodeOutputExecPin entry = Method.EntryNode.InitialExecutionPin;
        IfElseNode node = await AddIfElseAsync(new GraphPoint(0, 0), entry);
        Assert.Same(node.InputExecPins[0], entry.OutgoingPin);

        ClassContext.UndoRedo.Undo();
        Assert.Null(entry.OutgoingPin);

        ClassContext.UndoRedo.Redo();
        Assert.Same(node.InputExecPins[0], entry.OutgoingPin);
    }

    [Fact(Timeout = 60000)]
    public async Task UndoingTheAddOfASavedClassLeavesItSaved()
    {
        await AddIfElseAsync(new GraphPoint(0, 0));
        Assert.True(Class.IsDirty);

        ClassContext.UndoRedo.Undo();
        Assert.False(Class.IsDirty);

        ClassContext.UndoRedo.Redo();
        Assert.True(Class.IsDirty);
    }

    [Fact(Timeout = 60000)]
    public async Task DeletingANodeIsUndoneWithItsConnections()
    {
        NodeOutputExecPin entry = Method.EntryNode.InitialExecutionPin;
        IfElseNode node = await AddIfElseAsync(new GraphPoint(0, 0), entry);
        GraphUtil.ConnectExecPins(node.OutputExecPins[0], Method.MainReturnNode.ReturnPin);
        int connections = Graph.Connections.Count;

        await DeleteAsync(node);

        Assert.Equal("Delete node", ClassContext.UndoRedo.UndoName);
        Assert.DoesNotContain(node, Method.Nodes);
        Assert.Null(entry.OutgoingPin);

        ClassContext.UndoRedo.Undo();

        Assert.Contains(node, Method.Nodes);
        Assert.Same(node.InputExecPins[0], entry.OutgoingPin);
        Assert.Same(Method.MainReturnNode.ReturnPin, node.OutputExecPins[0].OutgoingPin);
        Assert.Equal(connections, Graph.Connections.Count);

        ClassContext.UndoRedo.Redo();

        Assert.DoesNotContain(node, Method.Nodes);
        Assert.Null(entry.OutgoingPin);
    }

    [Fact(Timeout = 60000)]
    public async Task SeveralDeletedNodesAreOneUndoStepThatRestoresTheConnectionsBetweenThem()
    {
        NodeOutputExecPin entry = Method.EntryNode.InitialExecutionPin;
        IfElseNode first = await AddIfElseAsync(new GraphPoint(0, 0), entry);
        IfElseNode second = await AddIfElseAsync(new GraphPoint(300, 0), first.OutputExecPins[0]);
        Assert.Same(second.InputExecPins[0], first.OutputExecPins[0].OutgoingPin);

        await DeleteAsync(first, second);

        Assert.Equal("Delete nodes", ClassContext.UndoRedo.UndoName);
        Assert.DoesNotContain(first, Method.Nodes);
        Assert.DoesNotContain(second, Method.Nodes);

        ClassContext.UndoRedo.Undo();

        Assert.Same(first.InputExecPins[0], entry.OutgoingPin);
        Assert.Same(second.InputExecPins[0], first.OutputExecPins[0].OutgoingPin);
        Assert.Equal(1, second.InputExecPins[0].IncomingPins.Count);
    }

    [Fact(Timeout = 60000)]
    public async Task UndoingADeleteOfASavedClassLeavesItSaved()
    {
        IfElseNode node = await AddIfElseAsync(new GraphPoint(0, 0));
        ClassContext.UndoRedo.MarkSaved();
        Class.MarkClean();

        await DeleteAsync(node);
        Assert.True(Class.IsDirty);

        ClassContext.UndoRedo.Undo();
        Assert.False(Class.IsDirty);
    }

    [Fact]
    public async Task DeletingOnlyTheEntryNodeRecordsNothing()
    {
        await DeleteAsync(Method.EntryNode);

        Assert.False(ClassContext.UndoRedo.CanUndo);
        Assert.Contains(Method.EntryNode, Method.Nodes);
    }

    [Fact(Timeout = 60000)]
    public async Task ADeleteThenTheAddUndoLeavesNoNodeAndNothingToUndo()
    {
        IfElseNode node = await AddIfElseAsync(new GraphPoint(0, 0));
        ClassContext.UndoRedo.Undo();
        ClassContext.UndoRedo.Redo();

        await DeleteAsync(node);
        ClassContext.UndoRedo.Undo();
        ClassContext.UndoRedo.Undo();

        Assert.DoesNotContain(node, Method.Nodes);
        Assert.False(ClassContext.UndoRedo.CanUndo);
    }
}
