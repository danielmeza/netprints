using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Editor.Messages;
using NetPrints.Editor.ViewModels;

namespace NetPrints.Editor.Tests.ViewModels;

[TestClass]
public class NodeGraphVMTests : GraphTestBase
{
    [TestMethod]
    public void NodesAndConnectionsTrackModel()
    {
        Assert.HasCount(2, Graph.Nodes);
        var connection = Graph.Connections.Single();
        Assert.AreEqual(PinKind.Exec, connection.Kind);
        Assert.AreSame(Method.EntryNode.InitialExecutionPin, connection.Source.Pin);

        var write = new CallMethodNode(Method, ConsoleWriteLine(StringType));
        Assert.HasCount(3, Graph.Nodes);

        GraphUtil.ConnectExecPins(Method.EntryNode.InitialExecutionPin, write.InputExecPins[0]);
        GraphUtil.ConnectExecPins(write.OutputExecPins[0], Method.MainReturnNode.ReturnPin);
        Assert.HasCount(2, Graph.Connections);

        var literal = LiteralNode.WithValue(Method, "x");
        GraphUtil.ConnectDataPins(literal.OutputDataPins[0], write.ArgumentPins[0]);
        Assert.HasCount(3, Graph.Connections);
        Assert.IsTrue(Graph.Connections.Any(c => c.Kind == PinKind.Data));

        var typed = new TypeNode(Method, IntType);
        var makeArray = new MakeArrayTypeNode(Method);
        GraphUtil.ConnectTypePins(typed.OutputTypePins[0], makeArray.InputTypePins[0]);
        Assert.IsTrue(Graph.Connections.Any(c => c.Kind == PinKind.Type));

        GraphUtil.DisconnectInputDataPin(write.ArgumentPins[0]);
        Assert.IsFalse(Graph.Connections.Any(c => c.Kind == PinKind.Data));

        Method.Nodes.Remove(makeArray);
        Assert.IsFalse(Graph.Nodes.Any(n => n.Node == makeArray));
    }

    [TestMethod]
    public void ConnectionStateSurvivesRebuild()
    {
        var connection = Graph.Connections.Single();
        connection.IsFaint = true;
        _ = new CallMethodNode(Method, ConsoleWriteLine(StringType));

        Assert.AreSame(connection, Graph.Connections.Single());
        Assert.IsTrue(connection.IsFaint);
    }

    [TestMethod]
    public void ConnectionCommands()
    {
        Graph.Connections.Single().Disconnect();
        Assert.IsEmpty(Graph.Connections);
        Assert.IsNull(Method.EntryNode.InitialExecutionPin.OutgoingPin);
    }

    [TestMethod]
    public void SelectionByClickBoxAndDeselect()
    {
        var entry = VmOf(Method.EntryNode);
        var ret = VmOf(Method.MainReturnNode);

        entry.Select();
        CollectionAssert.AreEqual(new[] { entry }, Graph.SelectedNodes.ToArray());

        Graph.Handle(new NodeSelectionMessage([ret], DeselectPrevious: false));
        Assert.HasCount(2, Graph.SelectedNodes.ToList());

        Graph.SelectNodes([ret], deselectPrevious: true);
        CollectionAssert.AreEqual(new[] { ret }, Graph.SelectedNodes.ToArray());

        Graph.DeselectNodesCommand.Execute(null);
        Assert.IsFalse(Graph.SelectedNodes.Any());
    }

    [TestMethod]
    public void SnapSelectedToGrid()
    {
        var entry = VmOf(Method.EntryNode);
        var ret = VmOf(Method.MainReturnNode);
        entry.Location = new GraphPoint(30, 59);
        ret.Location = new GraphPoint(100, 3);
        Graph.SelectNodes([entry, ret], deselectPrevious: true);

        Graph.SnapSelectedToGrid();

        Assert.AreEqual(new GraphPoint(28, 56), entry.Location);
        Assert.AreEqual(new GraphPoint(84, 0), ret.Location);
    }

    [TestMethod]
    public void AddNodeClampsAndConnectsToSuggestionPin()
    {
        var node = Graph.AddNode<IfElseNode>(new GraphPoint(-10, -5));
        Assert.AreEqual(0, node.PositionX);
        Assert.AreEqual(0, node.PositionY);

        var returnNode = Method.MainReturnNode;
        var connected = (IfElseNode)Graph.AddNode<IfElseNode>(new GraphPoint(10, 20), Method.EntryNode.InitialExecutionPin);
        Assert.AreSame(connected.InputExecPins[0], Method.EntryNode.InitialExecutionPin.OutgoingPin, "auto-connected (PAR-47)");
        _ = returnNode;
    }

    [TestMethod]
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
        Assert.AreEqual(other.Name, call.MethodSpecifier.Name);
        Assert.AreEqual(Class.Type, call.MethodSpecifier.DeclaringType);
        Assert.AreEqual(56, call.PositionX);

        var construct = (ConstructorNode)graph.Drop(ctor, new GraphPoint(84, 84));
        Assert.AreEqual(Class.Type, construct.ConstructorSpecifier.DeclaringType);

        graph.Drop(variable, new GraphPoint(112, 112));
        Assert.IsTrue(graph.GetSetChooser.IsOpen, "dropping a variable opens the Get/Set chooser (PAR-57)");
        Assert.AreEqual(new GraphPoint(112, 112), graph.GetSetChooser.Position);
        Assert.AreEqual(variable.Name, graph.GetSetChooser.Variable!.Name);
    }

    [TestMethod]
    public void NameWatermark()
    {
        Assert.AreEqual(Method.Name, Graph.Name);
        var classGraph = new NodeGraphVM(Class, ClassEditor);
        Assert.AreEqual("C", classGraph.Name);
        classGraph.Dispose();
    }
}
