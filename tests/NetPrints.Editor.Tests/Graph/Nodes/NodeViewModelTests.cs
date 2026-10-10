using NetPrints.Core;
using NetPrints.Editor.Controls;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Graph.Nodes;
using NetPrints.Editor.Graph.Pins;
using NetPrints.Editor.Icons;
using NetPrints.Editor.Tests.Graph;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Editor.UndoRedo;
using NetPrints.Graph;

namespace NetPrints.Editor.Tests.Graph.Nodes;

public class NodeViewModelTests(TestEditor editor) : GraphTestBase(editor)
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
            (new IfElseNode(Method), NodeVisualKind.IfElse),
            (new ForLoopNode(Method), NodeVisualKind.ForLoop),
            (new ExplicitCastNode(Method), NodeVisualKind.ExplicitCast),
            (new AwaitNode(Method), NodeVisualKind.Await),
        };

        foreach (var (node, kind) in expected)
        {
            Assert.Equal(kind, VmOf(node).VisualKind);
        }
    }

    [Fact]
    public void NoBuiltInNodeWithExecutionPinsKeepsTheDefaultGlyph()
    {
        var variable = new VariableSpecifier("Length", IntType, MemberVisibility.Public, MemberVisibility.Public, StringType, VariableModifiers.None);
        var ctor = Editor.Reflection.Provider.GetConstructors(TypeSpecifier.FromType<List<int>>()).First();
        Node[] nodes =
        [
            Method.EntryNode,
            Method.MainReturnNode,
            new CallMethodNode(Method, ConsoleWriteLine(StringType)),
            new ConstructorNode(Method, ctor),
            new VariableSetterNode(Method, variable),
            new ThrowNode(Method),
            new TernaryNode(Method),
            new IfElseNode(Method),
            new ForLoopNode(Method),
            new ExplicitCastNode(Method),
            new AwaitNode(Method),
            new MakeArrayNode(Method),
            new TypeOfNode(Method),
            new DefaultNode(Method),
            new LiteralNode(Method, IntType),
        ];

        var withExecPins = nodes.Where(node => node.InputExecPins.Count + node.OutputExecPins.Count > 0).ToList();

        Assert.True(withExecPins.Count >= 10);
        Assert.All(withExecPins, node => Assert.NotEqual(IconIds.NodeKindDefault, VmOf(node).KindIconId));
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
        Assert.Contains(nameof(NodeViewModel.Location), changed);

        Assert.Equal(0, node.ZIndex);
        node.Select();
        Assert.True(node.IsSelected);
        Assert.Equal(1, node.ZIndex);
    }

    [Fact]
    public void TheOverloadListHoldsEveryOverloadWithTheCurrentOneMarkedAndFirst()
    {
        var current = ConsoleWriteLine(StringType);
        var write = VmOf(new CallMethodNode(Method, current));

        Assert.True(write.ShowOverloads);
        var picker = Assert.IsType<MethodPickerListViewModel>(write.OverloadPicker);
        var items = picker.Rows.Select(row => Assert.IsType<MethodPickerItem>(row.Item)).ToList();
        Assert.All(items, item => Assert.Equal("WriteLine", item.Name));
        Assert.True(items.Count > 10);
        Assert.Equal(current, items[0].Method);
        Assert.True(items[0].IsCurrent);
        Assert.Single(items, item => item.IsCurrent);

        var rest = items.Skip(1).Select(item => (item.ParameterCount, item.Signature)).ToList();
        Assert.Equal(rest.OrderBy(entry => entry.ParameterCount).ThenBy(entry => entry.Signature, StringComparer.Ordinal), rest);
    }

    [Fact]
    public void AConstructorNodeOffersItsOtherConstructors()
    {
        var constructors = Editor.Reflection.Provider.GetConstructors(TypeSpecifier.FromType<List<int>>()).ToList();
        var node = VmOf(new ConstructorNode(Method, constructors[0]));

        var picker = Assert.IsType<MethodPickerListViewModel>(node.OverloadPicker);
        var items = picker.Rows.Select(row => Assert.IsType<MethodPickerItem>(row.Item)).ToList();
        Assert.Equal(constructors.Count, items.Count);
        Assert.Equal(constructors[0], items[0].Constructor);
        Assert.True(items[0].IsCurrent);
    }

    [Fact]
    public void PickingAnOverloadReplacesTheNodeAndOneUndoRestoresIt()
    {
        var write = VmOf(new CallMethodNode(Method, ConsoleWriteLine(StringType)));
        var picker = Assert.IsType<MethodPickerListViewModel>(write.OverloadPicker);
        var intRow = picker.Rows.Single(row => row.Item?.Method is { Parameters: [{ Value: var type }] } && type == IntType);
        picker.Selected = intRow;

        picker.PickCommand.Execute(null);

        Assert.Equal(intRow.Item?.Method, Method.Nodes.OfType<CallMethodNode>().Single().MethodSpecifier);
        ClassContext.UndoRedo.Undo();
        Assert.Equal(StringType, Method.Nodes.OfType<CallMethodNode>().Single().MethodSpecifier.Parameters[0].Value);
    }

    [Fact]
    public void PickingTheCurrentOverloadChangesNothing()
    {
        var write = VmOf(new CallMethodNode(Method, ConsoleWriteLine(StringType)));
        var picker = Assert.IsType<MethodPickerListViewModel>(write.OverloadPicker);
        var undoName = ClassContext.UndoRedo.UndoName;

        picker.Selected = picker.Rows[0];
        picker.PickCommand.Execute(null);

        Assert.Equal(undoName, ClassContext.UndoRedo.UndoName);
        Assert.Same(write.Node, Method.Nodes.OfType<CallMethodNode>().Single());
    }

    [Fact]
    public void TheButtonTooltipGivesTheCountAndTheCurrentSignature()
    {
        var write = VmOf(new CallMethodNode(Method, ConsoleWriteLine(StringType)));
        var picker = Assert.IsType<MethodPickerListViewModel>(write.OverloadPicker);

        Assert.Equal($"Overloads ({picker.Rows.Count}): void WriteLine(string value)", write.OverloadsTooltip);
    }

    [Fact]
    public void ThereIsNoButtonWithoutAnotherOverload()
    {
        var entry = VmOf(Method.EntryNode);

        Assert.False(entry.ShowOverloads);
        Assert.Null(entry.OverloadPicker);
    }

    [Fact]
    public void AMakeArrayNodeOffersItsOtherSizeModeThroughTheSameList()
    {
        var makeArray = VmOf(new MakeArrayNode(Method));
        var picker = Assert.IsType<MethodPickerListViewModel>(makeArray.OverloadPicker);

        Assert.Equal([ModelOperations.UseInitializerList, ModelOperations.UsePredefinedSize], picker.Rows.Select(row => row.Text).Order().ToList());
        Assert.Equal(ModelOperations.UseInitializerList, picker.Rows[0].Text);
        Assert.True(picker.Rows[0].IsCurrent);

        picker.Selected = picker.Rows[1];
        picker.PickCommand.Execute(null);

        Assert.True(((MakeArrayNode)makeArray.Node).UsePredefinedSize);
        var after = Assert.IsType<MethodPickerListViewModel>(makeArray.OverloadPicker);
        Assert.Equal(ModelOperations.UsePredefinedSize, after.Rows[0].Text);
        Assert.True(after.Rows[0].IsCurrent);
    }

    [Fact]
    public void PureCheckbox()
    {
        var voidCall = VmOf(new CallMethodNode(Method, ConsoleWriteLine(StringType)));
        Assert.False(voidCall.CanSetPure);
        voidCall.IsPure = true;
        Assert.False(voidCall.Node.IsPure);

        var call = VmOf(new CallMethodNode(Method, FindMethod(typeof(string), "ToUpperInvariant")));
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

        var classGraph = new NodeGraphViewModel(Class, ClassContext.Services);
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
