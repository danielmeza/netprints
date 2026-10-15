using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Nodify.Avalonia.Connections;
using NetPrints.Core;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Graph.Pins;
using NetPrints.Editor.UITests.Shell;
using NetPrints.Graph;

namespace NetPrints.Editor.UITests.Graph;

/// <summary>Owner feedback 2026-10-15: the exec wire into a node follows it when it moves after the node was rewired by an insertion.</summary>
public class InsertedNodeWireTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Connector ConnectorOf(EditorSession session, NodePinViewModel pin) =>
        session.Window.GetVisualDescendants().OfType<Connector>().Single(c => ReferenceEquals(c.DataContext, pin));

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task TheWireIntoANodeRewiredAfterAnInsertionFollowsItWhenItMoves()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var graph = session.GraphViewModel;
        var entry = graph.Nodes.Single(n => n.Node is MethodEntryNode);
        var write = graph.Nodes.Single(n => n.Node is CallMethodNode);
        var input = write.InputExecPins.Single();
        var ifElse = Assert.IsType<IfElseNode>(graph.AddNode<IfElseNode>(new GraphPoint(200, 200), entry.OutputExecPins.Single().Pin));
        await session.WaitForRenderedAsync(Token);
        Assert.False(input.IsConnected);
        Assert.False(ConnectorOf(session, input).IsConnected);

        Assert.True(graph.Connect(graph.Nodes.Single(n => n.Node == ifElse).OutputExecPins.First(), input));
        await session.WaitForRenderedAsync(Token);
        var connection = graph.Connections.Single(c => c.Target == input);
        Assert.True(input.IsConnected);
        Assert.True(ConnectorOf(session, input).IsConnected);
        GraphPoint before = input.Anchor;

        write.Location = new GraphPoint(write.Location.X + 200, write.Location.Y + 150);
        await session.WaitForRenderedAsync(Token);

        Assert.Equal(before.X + 200, connection.Target.Anchor.X, 2.0);
        Assert.Equal(before.Y + 150, connection.Target.Anchor.Y, 2.0);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task TheWireIntoANodeInsertedFromThePinDragFollowsItWhenItIsDragged()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var graph = session.GraphViewModel;
        var forLoop = Assert.IsType<ForLoopNode>(graph.AddNode<ForLoopNode>(new GraphPoint(28, 400)));
        await session.WaitForRenderedAsync(Token);

        await session.Graph.Node(forLoop.Name).Output("Loop").DragCableToAsync(await session.Graph.EmptyPointAsync(Token), Token);
        var search = await session.Graph.Search.WaitOpenAsync(Token);
        await search.FilterAsync("write line", r => r.Contains(" WriteLine ", StringComparison.Ordinal), Token);
        await search.PressEnterAsync(Token);
        await search.WaitClosedAsync(Token);
        await session.WaitForRenderedAsync(Token);

        var inserted = ((MethodGraph)graph.Graph).Nodes.OfType<CallMethodNode>().Single(n => n.InputExecPins[0].IncomingPins.Contains(forLoop.LoopPin));
        var node = graph.Nodes.Single(n => n.Node == inserted);
        var entry = node.InputExecPins.Single();
        var connection = graph.Connections.Single(c => c.Target.Pin == inserted.InputExecPins.Single());
        Assert.Same(entry, connection.Target);
        Assert.True(ConnectorOf(session, entry).IsConnected);
        GraphPoint before = entry.Anchor;
        GraphPoint start = node.Location;

        await session.Graph.Node(inserted.Name).MoveByAsync(-120, 60, Token);
        await session.WaitForRenderedAsync(Token);

        Assert.NotEqual(start, node.Location);
        Assert.Equal(before.X + node.Location.X - start.X, connection.Target.Anchor.X, 2.0);
        Assert.Equal(before.Y + node.Location.Y - start.Y, connection.Target.Anchor.Y, 2.0);
    }
}
