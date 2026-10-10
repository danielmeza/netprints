using NetPrints.Core;
using NetPrints.Editor.Graph.Nodes;
using NetPrints.Editor.Tests.Graph;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Graph;

namespace NetPrints.Editor.Tests.UndoRedo;

/// <summary>Changing a call's purity is one undo step that restores every execution wire and the Exception pin's data wires (FR-099).</summary>
public sealed class ThePurityCheckBoxGoesThroughUndoTests : GraphTestBase
{
    private readonly CallMethodNode max;
    private readonly CallMethodNode next;
    private readonly CallMethodNode handler;
    private readonly CallMethodNode errorSink;
    private readonly CallMethodNode resultSink;
    private readonly NodeViewModel vm;

    public ThePurityCheckBoxGoesThroughUndoTests(TestEditor editor)
        : base(editor)
    {
        max = new CallMethodNode(Method, FindMethod(typeof(Math), "Max", typeof(int), typeof(int)));
        next = new CallMethodNode(Method, ConsoleWriteLine(StringType));
        handler = new CallMethodNode(Method, ConsoleWriteLine(StringType));
        errorSink = new CallMethodNode(Method, ConsoleWriteLine(TypeSpecifier.FromType<Exception>()));
        resultSink = new CallMethodNode(Method, ConsoleWriteLine(IntType));
        GraphUtil.ConnectExecPins(Method.EntryNode.InitialExecutionPin, max.InputExecPins[0]);
        GraphUtil.ConnectExecPins(max.OutputExecPins[0], next.InputExecPins[0]);
        GraphUtil.ConnectExecPins(max.CatchPin ?? throw new InvalidOperationException("No Catch pin."), handler.InputExecPins[0]);
        GraphUtil.ConnectDataPins(max.ExceptionPin ?? throw new InvalidOperationException("No Exception pin."), errorSink.ArgumentPins[0]);
        GraphUtil.ConnectDataPins(max.ReturnValuePins[0], resultSink.ArgumentPins[0]);
        ClassContext.UndoRedo.Clear();
        ClassContext.UndoRedo.MarkSaved();
        Class.MarkClean();
        vm = VmOf(max);
    }

    private void AssertImpureWithEveryWire()
    {
        Assert.False(max.IsPure);
        Assert.Same(max.InputExecPins[0], Method.EntryNode.InitialExecutionPin.OutgoingPin);
        Assert.Same(next.InputExecPins[0], max.OutputExecPins[0].OutgoingPin);
        Assert.Same(handler.InputExecPins[0], max.CatchPin?.OutgoingPin);
        Assert.Same(max.ExceptionPin, errorSink.ArgumentPins[0].IncomingPin);
        Assert.Same(max.ReturnValuePins[0], resultSink.ArgumentPins[0].IncomingPin);
    }

    private void AssertPure()
    {
        Assert.True(max.IsPure);
        Assert.Empty(max.InputExecPins);
        Assert.Empty(max.OutputExecPins);
        Assert.Null(max.ExceptionPin);
        Assert.Null(errorSink.ArgumentPins[0].IncomingPin);
        Assert.Same(max.ReturnValuePins[0], resultSink.ArgumentPins[0].IncomingPin);
    }

    [Fact]
    public void TickingPureIsOneUndoStepThatMarksTheClassDirty()
    {
        vm.IsPure = true;

        AssertPure();
        Assert.Equal("Make pure", ClassContext.UndoRedo.UndoName);
        Assert.True(Class.IsDirty);
        Assert.True(vm.IsPure);
    }

    [Fact]
    public void UndoMakesTheCallImpureAgainWithEveryExecWireCatchIncludedAndTheExceptionWires()
    {
        vm.IsPure = true;

        ClassContext.UndoRedo.Undo();

        AssertImpureWithEveryWire();
        Assert.False(vm.IsPure);
        Assert.False(Class.IsDirty);
        Assert.False(ClassContext.UndoRedo.CanUndo);
    }

    [Fact]
    public void RedoRemovesThemAgain()
    {
        vm.IsPure = true;
        ClassContext.UndoRedo.Undo();

        ClassContext.UndoRedo.Redo();

        AssertPure();
        Assert.True(vm.IsPure);
    }

    [Fact]
    public void UntickingPureIsOneUndoStepToo()
    {
        vm.IsPure = true;
        ClassContext.UndoRedo.Clear();

        vm.IsPure = false;

        Assert.False(max.IsPure);
        Assert.Equal("Make impure", ClassContext.UndoRedo.UndoName);
        ClassContext.UndoRedo.Undo();
        AssertPure();
        ClassContext.UndoRedo.Redo();
        Assert.False(max.IsPure);
        Assert.Single(max.InputExecPins);
        Assert.Equal(2, max.OutputExecPins.Count);
    }

    [Fact]
    public void AVoidCallCannotBeMadePureAndRecordsNothing()
    {
        var write = new CallMethodNode(Method, ConsoleWriteLine(StringType));
        ClassContext.UndoRedo.Clear();

        VmOf(write).IsPure = true;

        Assert.False(write.IsPure);
        Assert.False(ClassContext.UndoRedo.CanUndo);
    }
}
