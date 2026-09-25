using NetPrints.Core;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Graph.Nodes;
using NetPrints.Editor.Graph.Pins;
using NetPrints.Editor.Tests.Graph;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Editor.UndoRedo;
using NetPrints.Graph;

namespace NetPrints.Editor.Tests.Graph.Nodes;

public class NodeVMTests(TestEditor editor) : GraphTestBase(editor)
{
    [Fact]
    public void VisualKindForEveryNodeKind()
    {
        var variable = new VariableSpecifier("Length", IntType, MemberVisibility.Public, MemberVisibility.Public, StringType, VariableModifiers.None);
        var toUpper = FindMethod(typeof(string), "ToUpperInvariant");
        var ctor = Editor.Reflection.Provider.GetConstructors(TypeSpecifier.FromType<List<int>>()).First();

        var expected = new (Node Node, NodeVisualKind Kind)[]
        {
            (Method.EntryNode, NodeVisualKind.Entry),
            (Method.MainReturnNode, NodeVisualKind.Return),
            (new CallMethodNode(Method, toUpper), NodeVisualKind.CallMethod),
            (new CallMethodNode(Method, ConsoleWriteLine(StringType)), NodeVisualKind.CallStatic),
            (new ConstructorNode(Method, ctor), NodeVisualKind.Constructor),
            (new MakeDelegateNode(Method, toUpper), NodeVisualKind.MakeDelegate),
            (new TypeNode(Method, IntType), NodeVisualKind.Type),
            (new MakeArrayTypeNode(Method), NodeVisualKind.Type),
            (new VariableGetterNode(Method, variable), NodeVisualKind.VariableGetter),
            (new VariableSetterNode(Method, variable), NodeVisualKind.VariableSetter),
            (new MakeArrayNode(Method), NodeVisualKind.MakeArray),
            (new ThrowNode(Method), NodeVisualKind.Throw),
            (new TernaryNode(Method), NodeVisualKind.Ternary),
            (new IfElseNode(Method), NodeVisualKind.Default),
        };

        foreach (var (node, kind) in expected)
        {
            Assert.Equal(kind, VmOf(node).VisualKind);
        }
    }

    [Fact]
    public void LocationSyncsWithModelAndSelectionRaisesZIndex()
    {
        var node = VmOf(Method.EntryNode);
        var changed = new List<string?>();
        node.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        node.Location = new GraphPoint(56, 84);
        Assert.Equal(56, Method.EntryNode.PositionX);
        Method.EntryNode.PositionY = 112;
        Assert.Equal(112, node.Location.Y);
        Assert.Contains(nameof(NodeVM.Location), changed);

        Assert.Equal(0, node.ZIndex);
        node.Select();
        Assert.True(node.IsSelected);
        Assert.Equal(1, node.ZIndex);
    }

    [Fact]
    public void OverloadsAndUndoableChange()
    {
        var write = VmOf(new CallMethodNode(Method, ConsoleWriteLine(StringType)));
        Assert.True(write.ShowOverloads);
        Assert.True(write.Overloads.OfType<MethodSpecifier>().All(m => m.Name == "WriteLine"));
        Assert.False(write.Overloads.Contains(write.CurrentOverload!), "the current overload is excluded");

        var intOverload = write.Overloads.OfType<MethodSpecifier>().First(m => m.Parameters.Count == 1 && m.Parameters[0].Value == IntType);
        write.SelectedOverload = intOverload;

        var replaced = Method.Nodes.OfType<CallMethodNode>().Single();
        Assert.Equal(intOverload, replaced.MethodSpecifier);
        Assert.Null(write.SelectedOverload);

        ClassEditor.UndoCommand.Execute(null);
        Assert.Equal(StringType, Method.Nodes.OfType<CallMethodNode>().Single().MethodSpecifier.Parameters[0].Value);

        var makeArray = VmOf(new MakeArrayNode(Method));
        Assert.Equal(new object[] { ModelOperations.UsePredefinedSize }, makeArray.Overloads.ToArray());
        makeArray.SelectedOverload = ModelOperations.UsePredefinedSize;
        Assert.True(((MakeArrayNode)makeArray.Node).UsePredefinedSize);
        Assert.Equal(new object[] { ModelOperations.UseInitializerList }, makeArray.Overloads.ToArray());
    }

    [Fact]
    public void PureCheckbox()
    {
        var call = VmOf(new CallMethodNode(Method, ConsoleWriteLine(StringType)));
        Assert.True(call.CanSetPure);
        Assert.False(call.IsPure);
        call.IsPure = true;
        Assert.True(call.Node.IsPure);
        Assert.Empty(call.InputExecPins);

        Assert.False(VmOf(Method.EntryNode).CanSetPure);
    }

    [Fact]
    public void PinButtonsAndTooltips()
    {
        var entry = VmOf(Method.EntryNode);
        Assert.True(entry.ShowLeftPinButtons);
        Assert.True(entry.ShowRightPinButtons);
        Assert.Equal("Add method parameter", entry.LeftPlusToolTip);
        Assert.Equal("Add method generic type parameter", entry.RightPlusToolTip);
        entry.LeftPinsPlusCommand.Execute(null);
        Assert.Equal(1, Method.ArgumentTypes.Count());
        Assert.Equal(1, entry.OutputDataPins.Count());
        entry.LeftPinsMinusCommand.Execute(null);
        Assert.Empty(Method.ArgumentTypes);
        entry.RightPinsPlusCommand.Execute(null);
        Assert.Equal(1, Method.MethodEntryNode.OutputTypePins.Count());
        entry.RightPinsMinusCommand.Execute(null);

        var ret = VmOf(Method.MainReturnNode);
        Assert.True(ret.ShowLeftPinButtons);
        Assert.Equal("Remove method return value", ret.LeftMinusToolTip);
        ret.LeftPinsPlusCommand.Execute(null);
        Assert.Equal(1, Method.ReturnTypes.Count());

        var extraReturn = VmOf(new ReturnNode(Method));
        Assert.False(extraReturn.ShowLeftPinButtons, "only the main return node has +/-");

        var array = VmOf(new MakeArrayNode(Method));
        Assert.Equal("Add array element", array.LeftPlusToolTip);
        int before = array.Node.InputDataPins.Count;
        array.LeftPinsPlusCommand.Execute(null);
        Assert.Equal(before + 1, array.Node.InputDataPins.Count);

        var classGraph = new NodeGraphVM(Class, ClassEditor);
        var classReturn = classGraph.Nodes.Single(n => n.Node is ClassReturnNode);
        Assert.True(classReturn.ShowLeftPinButtons);
        Assert.Equal("Add interface", classReturn.LeftPlusToolTip);
        classGraph.Dispose();
    }

    [Fact]
    public void InputsAndOutputsFollowPinChanges()
    {
        var entry = VmOf(Method.EntryNode);
        int outputs = entry.Outputs.Count;
        Method.MethodEntryNode.AddArgument();
        Assert.Equal(outputs + 1, entry.Outputs.Count);
        Assert.Equal(PinKind.Exec, entry.Outputs[0].Kind);
    }

    [Fact]
    public void RerouteNodeIsCompact()
    {
        Graph.Connections.Single().InsertReroute();
        var reroute = Graph.Nodes.Single(n => n.IsRerouteNode);
        Assert.True(reroute.AllPins.All(p => p.IsRerouteNodePin));
    }
}
