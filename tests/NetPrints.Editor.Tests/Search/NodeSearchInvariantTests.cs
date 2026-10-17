using NetPrints.Core;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Hosting;
using NetPrints.Graph;

namespace NetPrints.Editor.Tests.Search;

/// <summary>
/// Invariants of the node search that the provider, creation-service and ranker changes keep (FR-016, FR-017, US2 scenario 6):
/// opening and filtering the search creates no node and records no undo step.
/// </summary>
public sealed class NodeSearchInvariantTests(IReflectionHost sharedReflection)
    : FixtureGraphTestBase(sharedReflection)
{
    private NodePin?[] Pins()
    {
        var write = new CallMethodNode(Method, ConsoleWriteLine(StringType));
        return [null, write.InputExecPins[0], write.OutputExecPins[0], write.ArgumentPins[0], new TypeNode(Method, IntType).OutputTypePins[0]];
    }

    [Fact]
    public async Task OpeningTheSearchCreatesNoNodeAndLeavesTheUndoStackUnchanged()
    {
        foreach (NodePin? pin in Pins())
        {
            int nodes = Method.Nodes.Count;
            int viewModels = Graph.Nodes.Count;
            string? undoName = ClassContext.UndoRedo.UndoName;
            bool canUndo = ClassContext.UndoRedo.CanUndo;

            await Graph.OpenSearchAsync(new GraphPoint(10, 20), pin, TestContext.Current.CancellationToken);

            Assert.True(Graph.Search.IsOpen);
            Assert.Equal(nodes, Method.Nodes.Count);
            Assert.Equal(viewModels, Graph.Nodes.Count);
            Assert.Equal(canUndo, ClassContext.UndoRedo.CanUndo);
            Assert.Equal(undoName, ClassContext.UndoRedo.UndoName);
        }
    }

    [Fact]
    public async Task FilteringAndClosingTheSearchChangeNoGraph()
    {
        await Graph.OpenSearchAsync(new GraphPoint(0, 0), null, TestContext.Current.CancellationToken);
        int nodes = Method.Nodes.Count;
        bool canUndo = ClassContext.UndoRedo.CanUndo;

        Graph.Search.SearchText = "write line";
        Editor.Scheduler.AdvanceBy(Graph.Search.FilterThrottle.Ticks);
        Graph.Search.CloseCommand.Execute(null);

        Assert.False(Graph.Search.IsOpen);
        Assert.Equal(nodes, Method.Nodes.Count);
        Assert.Equal(canUndo, ClassContext.UndoRedo.CanUndo);
    }

    [Fact]
    public async Task TheRowsOfASecondOpenEqualTheRowsOfTheFirst()
    {
        await Graph.OpenSearchAsync(new GraphPoint(0, 0), null, TestContext.Current.CancellationToken);
        string[] first = Graph.Search.Items.Select(row => row.Text).ToArray();

        await Graph.OpenSearchAsync(new GraphPoint(5, 5), null, TestContext.Current.CancellationToken);

        Assert.Equal(first, Graph.Search.Items.Select(row => row.Text).ToArray());
    }
}
