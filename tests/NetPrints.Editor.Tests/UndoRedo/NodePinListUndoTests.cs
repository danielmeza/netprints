using NetPrints.Core;
using NetPrints.Editor.Graph.Nodes;
using NetPrints.Editor.Tests.Graph;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Graph;

namespace NetPrints.Editor.Tests.UndoRedo;

/// <summary>Adding or removing a pin in a node's editable pin list is one undo step that marks the class dirty (FR-098).</summary>
public sealed class NodePinListUndoTests : GraphTestBase
{
    public NodePinListUndoTests(TestEditor editor)
        : base(editor)
    {
        ClassContext.UndoRedo.MarkSaved();
        Class.MarkClean();
    }

    [Fact]
    public void AddingAnArrayElementIsOneUndoStepAndMarksTheClassDirty()
    {
        var array = new MakeArrayNode(Method);
        ClassContext.UndoRedo.Clear();
        ClassContext.UndoRedo.MarkSaved();
        Class.MarkClean();
        NodeViewModel vm = VmOf(array);

        vm.LeftPinsPlusCommand.Execute(null);

        Assert.Single(array.InputDataPins);
        Assert.Equal(vm.LeftPlusToolTip, ClassContext.UndoRedo.UndoName);
        Assert.True(Class.IsDirty);
        NodeInputDataPin pin = array.InputDataPins[0];

        ClassContext.UndoRedo.Undo();
        Assert.Empty(array.InputDataPins);
        Assert.Empty(vm.InputDataPins);
        Assert.False(Class.IsDirty);

        ClassContext.UndoRedo.Redo();
        Assert.Same(pin, Assert.Single(array.InputDataPins));
        Assert.Single(vm.InputDataPins);
        Assert.True(Class.IsDirty);
    }

    [Fact]
    public void RemovingAConnectedArrayElementIsUndoneWithItsConnection()
    {
        var upper = new CallMethodNode(Method, FindMethod(typeof(string), "ToUpperInvariant"));
        var array = new MakeArrayNode(Method);
        array.AddElementPin();
        NodeInputDataPin pin = array.InputDataPins[0];
        GraphUtil.ConnectDataPins(upper.ReturnValuePins[0], pin);
        ClassContext.UndoRedo.Clear();
        NodeViewModel vm = VmOf(array);

        vm.LeftPinsMinusCommand.Execute(null);

        Assert.Empty(array.InputDataPins);
        Assert.Empty(upper.ReturnValuePins[0].OutgoingPins);
        Assert.Equal(vm.LeftMinusToolTip, ClassContext.UndoRedo.UndoName);

        ClassContext.UndoRedo.Undo();
        Assert.Same(pin, Assert.Single(array.InputDataPins));
        Assert.Same(upper.ReturnValuePins[0], pin.IncomingPin);
        Assert.Contains(pin, upper.ReturnValuePins[0].OutgoingPins);

        ClassContext.UndoRedo.Redo();
        Assert.Empty(array.InputDataPins);
        Assert.Empty(upper.ReturnValuePins[0].OutgoingPins);
    }

    [Fact]
    public void RemovingAMethodParameterIsUndoneWithItsTypeAndDataConnections()
    {
        var type = new TypeNode(Method, IntType);
        var consumer = new CallMethodNode(Method, ConsoleWriteLine(IntType));
        MethodEntryNode entry = Method.MethodEntryNode;
        entry.AddArgument();
        NodeOutputDataPin parameter = entry.OutputDataPins[0];
        NodeInputTypePin typePin = entry.InputTypePins[0];
        GraphUtil.ConnectTypePins(type.OutputTypePins[0], typePin);
        GraphUtil.ConnectDataPins(parameter, consumer.ArgumentPins[0]);
        ClassContext.UndoRedo.Clear();
        NodeViewModel vm = VmOf(entry);

        vm.LeftPinsMinusCommand.Execute(null);

        Assert.Empty(entry.OutputDataPins);
        Assert.Empty(entry.InputTypePins);
        Assert.Null(consumer.ArgumentPins[0].IncomingPin);

        ClassContext.UndoRedo.Undo();
        Assert.Same(parameter, Assert.Single(entry.OutputDataPins));
        Assert.Same(typePin, Assert.Single(entry.InputTypePins));
        Assert.Same(parameter, consumer.ArgumentPins[0].IncomingPin);
        Assert.Same(type.OutputTypePins[0], typePin.IncomingPin);
        Assert.Equal(IntType, parameter.PinType.Value);

        ClassContext.UndoRedo.Redo();
        Assert.Empty(entry.OutputDataPins);
        Assert.Null(consumer.ArgumentPins[0].IncomingPin);
    }

    [Fact]
    public void AddingAMethodParameterAndAGenericArgumentAreUndoable()
    {
        MethodEntryNode entry = Method.MethodEntryNode;
        ClassContext.UndoRedo.Clear();
        NodeViewModel vm = VmOf(entry);

        vm.LeftPinsPlusCommand.Execute(null);
        vm.RightPinsPlusCommand.Execute(null);
        Assert.Single(entry.OutputDataPins);
        Assert.Single(entry.OutputTypePins);

        ClassContext.UndoRedo.Undo();
        Assert.Empty(entry.OutputTypePins);
        Assert.Single(entry.OutputDataPins);

        ClassContext.UndoRedo.Undo();
        Assert.Empty(entry.OutputDataPins);
        Assert.Empty(entry.InputTypePins);
        Assert.False(ClassContext.UndoRedo.CanUndo);
    }

    [Fact]
    public void ReturnValuesAreUndoable()
    {
        ReturnNode main = Method.MainReturnNode;
        var source = new CallMethodNode(Method, FindMethod(typeof(string), "ToUpperInvariant"));
        main.AddReturnType();
        NodeInputDataPin pin = main.InputDataPins[0];
        GraphUtil.ConnectDataPins(source.ReturnValuePins[0], pin);
        ClassContext.UndoRedo.Clear();
        NodeViewModel vm = VmOf(main);

        vm.LeftPinsMinusCommand.Execute(null);
        Assert.Empty(main.InputDataPins);

        ClassContext.UndoRedo.Undo();
        Assert.Same(pin, Assert.Single(main.InputDataPins));
        Assert.Same(source.ReturnValuePins[0], pin.IncomingPin);

        vm.LeftPinsPlusCommand.Execute(null);
        Assert.Equal(2, main.InputDataPins.Count);
        ClassContext.UndoRedo.Undo();
        Assert.Single(main.InputDataPins);
    }

    [Fact]
    public void RemovingFromAnEmptyListRecordsNothing()
    {
        var array = new MakeArrayNode(Method);
        ClassContext.UndoRedo.Clear();

        VmOf(array).LeftPinsMinusCommand.Execute(null);

        Assert.False(ClassContext.UndoRedo.CanUndo);
    }
}
