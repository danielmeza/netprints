using NetPrints.Core;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Tests.Graph;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Graph;

namespace NetPrints.Editor.Tests.Search;

/// <summary>Opening the node search from a pin leaves the graph alone until a node is picked; picking is one undo step (FR-097).</summary>
public sealed class NodeSearchConnectionTests(IReflectionHost sharedReflection)
    : FixtureGraphTestBase(sharedReflection)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private (NodeOutputExecPin Entry, CallMethodNode Write) ConnectedEntry()
    {
        var write = new CallMethodNode(Method, ConsoleWriteLine(StringType));
        GraphUtil.ConnectExecPins(Method.EntryNode.InitialExecutionPin, write.InputExecPins[0]);
        ClassContext.UndoRedo.Clear();
        return (Method.EntryNode.InitialExecutionPin, write);
    }

    [Fact(Timeout = 60000)]
    public async Task OpeningTheSearchFromAConnectedExecOutputKeepsTheConnection()
    {
        var (entry, write) = ConnectedEntry();

        await Graph.OpenSearchAsync(new GraphPoint(0, 0), entry, Token);

        Assert.Same(write.InputExecPins[0], entry.OutgoingPin);
        Assert.False(ClassContext.UndoRedo.CanUndo);
    }

    [Fact(Timeout = 60000)]
    public async Task ClosingTheSearchWithoutAPickChangesNothing()
    {
        var (entry, write) = ConnectedEntry();
        await Graph.OpenSearchAsync(new GraphPoint(0, 0), entry, Token);

        Graph.Search.CloseCommand.Execute(null);

        Assert.False(Graph.Search.IsOpen);
        Assert.Same(write.InputExecPins[0], entry.OutgoingPin);
        Assert.False(ClassContext.UndoRedo.CanUndo);
    }

    [Fact(Timeout = 60000)]
    public async Task PickingANodeReplacesTheConnectionAsOneUndoStep()
    {
        var (entry, write) = ConnectedEntry();
        await Graph.OpenSearchAsync(new GraphPoint(0, 0), entry, Token);

        await Graph.Search.SelectCommand.ExecuteAsync(Graph.Search.AllSuggestions.First(item => item.Text == "If Else"));

        IfElseNode node = Method.Nodes.OfType<IfElseNode>().Single();
        Assert.Same(node.InputExecPins[0], entry.OutgoingPin);
        Assert.Empty(write.InputExecPins[0].IncomingPins);

        ClassContext.UndoRedo.Undo();
        Assert.DoesNotContain(node, Method.Nodes);
        Assert.Same(write.InputExecPins[0], entry.OutgoingPin);
        Assert.False(ClassContext.UndoRedo.CanUndo);

        ClassContext.UndoRedo.Redo();
        Assert.Same(node.InputExecPins[0], entry.OutgoingPin);
        Assert.Empty(write.InputExecPins[0].IncomingPins);
    }

    [Fact(Timeout = 60000)]
    public async Task UndoingAnInsertionFromADataPinRestoresTheExecChainAndTheOldValueWire()
    {
        var (entry, write) = ConnectedEntry();
        await Graph.OpenSearchAsync(new GraphPoint(0, 0), write.ArgumentPins[0], Token);

        await Graph.Search.SelectCommand.ExecuteAsync(Graph.Search.AllSuggestions.First(item => item.Text.Contains("ReadLine", StringComparison.Ordinal)));

        CallMethodNode read = Method.Nodes.OfType<CallMethodNode>().Single(node => node.MethodName == "ReadLine");
        Assert.Same(read.InputExecPins[0], entry.OutgoingPin);
        Assert.Same(write.InputExecPins[0], read.OutputExecPins[0].OutgoingPin);

        ClassContext.UndoRedo.Undo();
        Assert.DoesNotContain(read, Method.Nodes);
        Assert.Same(write.InputExecPins[0], entry.OutgoingPin);
        Assert.Null(write.ArgumentPins[0].IncomingPin);

        ClassContext.UndoRedo.Redo();
        Assert.Same(read.InputExecPins[0], entry.OutgoingPin);
        Assert.Same(read.ReturnValuePins[0], write.ArgumentPins[0].IncomingPin);
    }
}
