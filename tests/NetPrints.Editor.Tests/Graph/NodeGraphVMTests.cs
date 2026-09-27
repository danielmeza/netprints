using NetPrints.Core;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Graph.Pins;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Graph;

namespace NetPrints.Editor.Tests.Graph;

public class NodeGraphVMTests(TestEditor editor) : GraphTestBase(editor)
{
    [Fact]
    public void NodesAndConnectionsTrackModel()
    {
        Assert.Equal(2, Graph.Nodes.Count());
        var connection = Graph.Connections.Single();
        Assert.Equal(PinKind.Exec, connection.Kind);
        Assert.Same(Method.EntryNode.InitialExecutionPin, connection.Source.Pin);

        var write = new CallMethodNode(Method, ConsoleWriteLine(StringType));
        Assert.Equal(3, Graph.Nodes.Count());

        GraphUtil.ConnectExecPins(Method.EntryNode.InitialExecutionPin, write.InputExecPins[0]);
        GraphUtil.ConnectExecPins(write.OutputExecPins[0], Method.MainReturnNode.ReturnPin);
        Assert.Equal(2, Graph.Connections.Count());

        var literal = LiteralNode.WithValue(Method, "x");
        GraphUtil.ConnectDataPins(literal.OutputDataPins[0], write.ArgumentPins[0]);
        Assert.Equal(3, Graph.Connections.Count());
        Assert.True(Graph.Connections.Any(c => c.Kind == PinKind.Data));

        var typed = new TypeNode(Method, IntType);
        var makeArray = new MakeArrayTypeNode(Method);
        GraphUtil.ConnectTypePins(typed.OutputTypePins[0], makeArray.InputTypePins[0]);
        Assert.True(Graph.Connections.Any(c => c.Kind == PinKind.Type));

        GraphUtil.DisconnectInputDataPin(write.ArgumentPins[0]);
        Assert.False(Graph.Connections.Any(c => c.Kind == PinKind.Data));

        Method.Nodes.Remove(makeArray);
        Assert.False(Graph.Nodes.Any(n => n.Node == makeArray));
    }

    [Fact]
    public void ConnectionStateSurvivesRebuild()
    {
        var connection = Graph.Connections.Single();
        connection.IsFaint = true;
        _ = new CallMethodNode(Method, ConsoleWriteLine(StringType));

        Assert.Same(connection, Graph.Connections.Single());
        Assert.True(connection.IsFaint);
    }

    [Fact]
    public void ConnectionCommands()
    {
        Graph.Connections.Single().Disconnect();
        Assert.Empty(Graph.Connections);
        Assert.Null(Method.EntryNode.InitialExecutionPin.OutgoingPin);
    }

    [Fact]
    public void SelectionByClickBoxAndDeselect()
    {
        var entry = VmOf(Method.EntryNode);
        var ret = VmOf(Method.MainReturnNode);

        entry.Select();
        Assert.Equal(new[] { entry }, Graph.SelectedNodes.ToArray());

        Graph.SelectNodes([ret], deselectPrevious: false);
        Assert.Equal(2, Graph.SelectedNodes.ToList().Count());

        Graph.SelectNodes([ret], deselectPrevious: true);
        Assert.Equal(new[] { ret }, Graph.SelectedNodes.ToArray());

        Graph.DeselectNodesCommand.Execute(null);
        Assert.False(Graph.SelectedNodes.Any());
    }

    [Fact]
    public void AddNodeClampsAndConnectsToSuggestionPin()
    {
        var node = Graph.AddNode<IfElseNode>(new GraphPoint(-10, -5));
        Assert.Equal(0, node.PositionX);
        Assert.Equal(0, node.PositionY);

        var returnNode = Method.MainReturnNode;
        var connected = (IfElseNode)Graph.AddNode<IfElseNode>(new GraphPoint(10, 20), Method.EntryNode.InitialExecutionPin);
        Assert.Same(connected.InputExecPins[0], Method.EntryNode.InitialExecutionPin.OutgoingPin);
        _ = returnNode;
    }

    [Fact]
    public void DropMethodConstructorAndVariable()
    {
        ClassEditor.CreateMethodCommand.Execute(null);
        var other = ClassEditor.Methods.Last();
        ClassEditor.CreateConstructorCommand.Execute(null);
        var ctor = ClassEditor.Constructors.Single();
        ClassEditor.CreateVariableCommand.Execute(null);
        var variable = ClassEditor.Variables.Single();

        ClassEditor.OpenGraph(Method);
        var graph = ClassEditor.OpenedGraph!;

        var call = (CallMethodNode)graph.Drop(other, new GraphPoint(56, 56));
        Assert.Equal(other.Name, call.MethodSpecifier.Name);
        Assert.Equal(Class.Type, call.MethodSpecifier.DeclaringType);
        Assert.Equal(56, call.PositionX);

        var construct = (ConstructorNode)graph.Drop(ctor, new GraphPoint(84, 84));
        Assert.Equal(Class.Type, construct.ConstructorSpecifier.DeclaringType);

        graph.Drop(variable, new GraphPoint(112, 112));
        Assert.True(graph.GetSetChooser.IsOpen, "dropping a variable opens the Get/Set chooser (PAR-57)");
        Assert.Equal(new GraphPoint(112, 112), graph.GetSetChooser.Position);
        Assert.Equal(variable.Name, graph.GetSetChooser.Variable!.Name);
    }

    [Fact]
    public void NameWatermark()
    {
        Assert.Equal(Method.Name, Graph.Name);
        var classGraph = new NodeGraphVM(Class, ClassEditor);
        Assert.Equal("C", classGraph.Name);
        classGraph.Dispose();
    }
}
