using NetPrints.Core;
using NetPrints.Editor.Tests.Graph;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Graph;

namespace NetPrints.Editor.Tests.UndoRedo;

/// <summary>Editing a method's parameters from its entry node moves the calls to the method onto the new signature, in one undo step (T094b, Review F R3).</summary>
public sealed class MethodParameterEditTests : GraphTestBase
{
    private readonly MethodGraph caller;

    public MethodParameterEditTests(TestEditor editor)
        : base(editor)
    {
        caller = ClassContext.CreateMethod();
        ClassContext.UndoRedo.Clear();
    }

    private MethodSpecifier SpecifierOfTheMethod() =>
        new(Method.Name,
            Method.NamedArgumentTypes.Select(argument => new MethodParameter(argument.Name, argument.Value, MethodParameterPassType.Default, false, null)),
            Method.ReturnTypes.Cast<TypeSpecifier>(), Method.Modifiers, Method.Visibility, Class.Type, []);

    private CallMethodNode TheCall() => caller.Nodes.OfType<CallMethodNode>().Single();

    [Fact]
    public void AddingAParameterMovesTheCallsToTheNewSignatureAndUndoesInOneStep()
    {
        _ = new CallMethodNode(caller, SpecifierOfTheMethod());

        VmOf(Method.MethodEntryNode).LeftPinsPlusCommand.Execute(null);

        Assert.Single(TheCall().ArgumentPins);
        ClassContext.UndoRedo.Undo();
        Assert.Empty(TheCall().ArgumentPins);
        Assert.Empty(Method.MethodEntryNode.OutputDataPins);
        Assert.False(ClassContext.UndoRedo.CanUndo);

        ClassContext.UndoRedo.Redo();
        Assert.Single(TheCall().ArgumentPins);
        Assert.Single(Method.MethodEntryNode.OutputDataPins);
    }

    [Fact]
    public void RemovingAParameterMovesTheCallsToTheNewSignatureAndUndoesInOneStep()
    {
        Method.MethodEntryNode.AddArgument();
        _ = new CallMethodNode(caller, SpecifierOfTheMethod());
        ClassContext.UndoRedo.Clear();

        VmOf(Method.MethodEntryNode).LeftPinsMinusCommand.Execute(null);

        Assert.Empty(TheCall().ArgumentPins);
        ClassContext.UndoRedo.Undo();
        Assert.Single(TheCall().ArgumentPins);
        Assert.Single(Method.MethodEntryNode.OutputDataPins);
        Assert.False(ClassContext.UndoRedo.CanUndo);

        ClassContext.UndoRedo.Redo();
        Assert.Empty(TheCall().ArgumentPins);
    }

    [Fact]
    public void RetypingAParameterByConnectingATypeNodeMovesTheCallsAndUndoesInOneStep()
    {
        Method.MethodEntryNode.AddArgument();
        _ = new CallMethodNode(caller, SpecifierOfTheMethod());
        var type = new TypeNode(Method, IntType);
        ClassContext.UndoRedo.Clear();

        Assert.True(VmOf(type.OutputTypePins[0]).ConnectTo(VmOf(Method.MethodEntryNode.InputTypePins[0])));

        Assert.Equal(IntType, TheCall().ArgumentPins[0].PinType.Value);
        Assert.Equal(IntType, Method.MethodEntryNode.OutputDataPins[0].PinType.Value);

        ClassContext.UndoRedo.Undo();
        Assert.Equal(TypeSpecifier.FromType<object>(), TheCall().ArgumentPins[0].PinType.Value);
        Assert.Null(Method.MethodEntryNode.InputTypePins[0].IncomingPin);
        Assert.False(ClassContext.UndoRedo.CanUndo);

        ClassContext.UndoRedo.Redo();
        Assert.Equal(IntType, TheCall().ArgumentPins[0].PinType.Value);
        Assert.Same(type.OutputTypePins[0], Method.MethodEntryNode.InputTypePins[0].IncomingPin);
    }

    [Fact]
    public void DisconnectingTheTypeOfAParameterMovesTheCallsBackToObject()
    {
        var type = new TypeNode(Method, IntType);
        Method.MethodEntryNode.AddArgument();
        GraphUtil.ConnectTypePins(type.OutputTypePins[0], Method.MethodEntryNode.InputTypePins[0]);
        _ = new CallMethodNode(caller, SpecifierOfTheMethod());
        ClassContext.UndoRedo.Clear();

        VmOf(Method.MethodEntryNode.InputTypePins[0]).DisconnectAllCommand.Execute(null);

        Assert.Equal(TypeSpecifier.FromType<object>(), TheCall().ArgumentPins[0].PinType.Value);
        ClassContext.UndoRedo.Undo();
        Assert.Equal(IntType, TheCall().ArgumentPins[0].PinType.Value);
        Assert.Same(type.OutputTypePins[0], Method.MethodEntryNode.InputTypePins[0].IncomingPin);
    }
}
