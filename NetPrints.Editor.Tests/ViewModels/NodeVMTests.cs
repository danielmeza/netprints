using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Graph.Nodes;
using NetPrints.Editor.Graph.Pins;
using NetPrints.Editor.UndoRedo;

namespace NetPrints.Editor.Tests.ViewModels;

[TestClass]
public class NodeVMTests : GraphTestBase
{
    [TestMethod]
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
            Assert.AreEqual(kind, VmOf(node).VisualKind, node.GetType().Name);
        }
    }

    [TestMethod]
    public void LocationSyncsWithModelAndSelectionRaisesZIndex()
    {
        var node = VmOf(Method.EntryNode);
        var changed = new List<string?>();
        node.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        node.Location = new GraphPoint(56, 84);
        Assert.AreEqual(56, Method.EntryNode.PositionX);
        Method.EntryNode.PositionY = 112;
        Assert.AreEqual(112, node.Location.Y);
        CollectionAssert.Contains(changed, nameof(NodeVM.Location));

        Assert.AreEqual(0, node.ZIndex);
        node.Select();
        Assert.IsTrue(node.IsSelected);
        Assert.AreEqual(1, node.ZIndex);
    }

    [TestMethod]
    public void OverloadsAndUndoableChange()
    {
        var write = VmOf(new CallMethodNode(Method, ConsoleWriteLine(StringType)));
        Assert.IsTrue(write.ShowOverloads);
        Assert.IsTrue(write.Overloads.OfType<MethodSpecifier>().All(m => m.Name == "WriteLine"));
        Assert.IsFalse(write.Overloads.Contains(write.CurrentOverload!), "the current overload is excluded");

        var intOverload = write.Overloads.OfType<MethodSpecifier>().First(m => m.Parameters.Count == 1 && m.Parameters[0].Value == IntType);
        write.SelectedOverload = intOverload;

        var replaced = Method.Nodes.OfType<CallMethodNode>().Single();
        Assert.AreEqual(intOverload, replaced.MethodSpecifier);
        Assert.IsNull(write.SelectedOverload, "the chooser resets");

        ClassEditor.UndoCommand.Execute(null);
        Assert.AreEqual(StringType, Method.Nodes.OfType<CallMethodNode>().Single().MethodSpecifier.Parameters[0].Value);

        var makeArray = VmOf(new MakeArrayNode(Method));
        CollectionAssert.AreEqual(new object[] { ModelOperations.UsePredefinedSize }, makeArray.Overloads.ToArray());
        makeArray.SelectedOverload = ModelOperations.UsePredefinedSize;
        Assert.IsTrue(((MakeArrayNode)makeArray.Node).UsePredefinedSize);
        CollectionAssert.AreEqual(new object[] { ModelOperations.UseInitializerList }, makeArray.Overloads.ToArray());
    }

    [TestMethod]
    public void PureCheckbox()
    {
        var call = VmOf(new CallMethodNode(Method, ConsoleWriteLine(StringType)));
        Assert.IsTrue(call.CanSetPure);
        Assert.IsFalse(call.IsPure);
        call.IsPure = true;
        Assert.IsTrue(call.Node.IsPure);
        Assert.IsEmpty(call.InputExecPins);

        Assert.IsFalse(VmOf(Method.EntryNode).CanSetPure);
    }

    [TestMethod]
    public void PinButtonsAndTooltips()
    {
        var entry = VmOf(Method.EntryNode);
        Assert.IsTrue(entry.ShowLeftPinButtons);
        Assert.IsTrue(entry.ShowRightPinButtons);
        Assert.AreEqual("Add method parameter", entry.LeftPlusToolTip);
        Assert.AreEqual("Add method generic type parameter", entry.RightPlusToolTip);
        entry.LeftPinsPlusCommand.Execute(null);
        Assert.HasCount(1, Method.ArgumentTypes);
        Assert.HasCount(1, entry.OutputDataPins);
        entry.LeftPinsMinusCommand.Execute(null);
        Assert.IsEmpty(Method.ArgumentTypes);
        entry.RightPinsPlusCommand.Execute(null);
        Assert.HasCount(1, Method.MethodEntryNode.OutputTypePins);
        entry.RightPinsMinusCommand.Execute(null);

        var ret = VmOf(Method.MainReturnNode);
        Assert.IsTrue(ret.ShowLeftPinButtons);
        Assert.AreEqual("Remove method return value", ret.LeftMinusToolTip);
        ret.LeftPinsPlusCommand.Execute(null);
        Assert.HasCount(1, Method.ReturnTypes);

        var extraReturn = VmOf(new ReturnNode(Method));
        Assert.IsFalse(extraReturn.ShowLeftPinButtons, "only the main return node has +/-");

        var array = VmOf(new MakeArrayNode(Method));
        Assert.AreEqual("Add array element", array.LeftPlusToolTip);
        int before = array.Node.InputDataPins.Count;
        array.LeftPinsPlusCommand.Execute(null);
        Assert.AreEqual(before + 1, array.Node.InputDataPins.Count);

        var classGraph = new NodeGraphVM(Class, ClassEditor);
        var classReturn = classGraph.Nodes.Single(n => n.Node is ClassReturnNode);
        Assert.IsTrue(classReturn.ShowLeftPinButtons);
        Assert.AreEqual("Add interface", classReturn.LeftPlusToolTip);
        classGraph.Dispose();
    }

    [TestMethod]
    public void InputsAndOutputsFollowPinChanges()
    {
        var entry = VmOf(Method.EntryNode);
        int outputs = entry.Outputs.Count;
        Method.MethodEntryNode.AddArgument();
        Assert.AreEqual(outputs + 1, entry.Outputs.Count);
        Assert.AreEqual(PinKind.Exec, entry.Outputs[0].Kind, "exec pins come first");
    }

    [TestMethod]
    public void RerouteNodeIsCompact()
    {
        Graph.Connections.Single().InsertReroute();
        var reroute = Graph.Nodes.Single(n => n.IsRerouteNode);
        Assert.IsTrue(reroute.AllPins.All(p => p.IsRerouteNodePin));
    }
}
