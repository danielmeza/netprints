using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Core;
using NetPrints.Editor.Tests.Graph;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Graph;
using NetPrints.Projects;
using NetPrints.Serialization;
using NetPrints.Serialization.Mapping;

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
            Method.ReturnTypes.Cast<TypeSpecifier>(), Method.Modifiers, Method.Visibility, Class.Type, [.. Method.GenericArgumentTypes]);

    private CallMethodNode TheCall() => caller.Nodes.OfType<CallMethodNode>().Single(call => call.MethodName == Method.Name);

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

    private CallMethodNode SavedCall()
    {
        var mapper = new DocumentMapper(new NodeDocumentConverterRegistry(NodeDocumentConverterRegistry.BuiltIn, []), NullLogger<DocumentMapper>.Instance);
        List<DocumentIssue> issues = [];
        ClassGraph loaded = mapper.FromDocument(mapper.ToDocument(Class), Project.FromSnapshot(TestSnapshots.Empty("P", "N")), issues, new DocumentId("C.netpc.json"));
        Assert.Empty(issues);
        return loaded.Methods[Class.Methods.IndexOf(caller)].Nodes.OfType<CallMethodNode>().Single(call => call.MethodName == Method.Name);
    }

    private void AssertTheCallsFollowTheSignature()
    {
        var entry = Method.MethodEntryNode;
        Assert.Equal(entry.OutputDataPins.Select(pin => pin.PinType.Value), TheCall().ArgumentPins.Select(pin => pin.PinType.Value));
        Assert.Equal(Method.ReturnTypes, TheCall().ReturnValuePins.Select(pin => pin.PinType.Value));
        Assert.Equal(Method.GenericArgumentTypes.Count(), TheCall().InputTypePins.Count);

        CallMethodNode saved = SavedCall();
        Assert.Equal(TheCall().ArgumentPins.Select(pin => pin.PinType.Value), saved.ArgumentPins.Select(pin => pin.PinType.Value));
        Assert.Equal(TheCall().ReturnValuePins.Select(pin => pin.PinType.Value), saved.ReturnValuePins.Select(pin => pin.PinType.Value));
        Assert.Equal(TheCall().InputTypePins.Count, saved.InputTypePins.Count);
    }

    [Fact]
    public void RetypingThroughTheWiredTypeNodeMovesTheCallsAndUndoesInOneStep()
    {
        var list = new TypeNode(Method, TypeSpecifier.FromType(typeof(List<>)));
        Method.MethodEntryNode.AddArgument();
        GraphUtil.ConnectTypePins(list.OutputTypePins[0], Method.MethodEntryNode.InputTypePins[0]);
        _ = new CallMethodNode(caller, SpecifierOfTheMethod());
        BaseType? before = TheCall().ArgumentPins[0].PinType.Value;
        var element = new TypeNode(Method, IntType);
        ClassContext.UndoRedo.Clear();

        Assert.True(VmOf(element.OutputTypePins[0]).ConnectTo(VmOf(list.InputTypePins[0])));

        Assert.NotEqual(before, TheCall().ArgumentPins[0].PinType.Value);
        AssertTheCallsFollowTheSignature();

        ClassContext.UndoRedo.Undo();
        Assert.Equal(before, TheCall().ArgumentPins[0].PinType.Value);
        Assert.Equal(before, Method.MethodEntryNode.OutputDataPins[0].PinType.Value);
        Assert.Null(list.InputTypePins[0].IncomingPin);
        Assert.False(ClassContext.UndoRedo.CanUndo);
        AssertTheCallsFollowTheSignature();

        ClassContext.UndoRedo.Redo();
        Assert.NotEqual(before, TheCall().ArgumentPins[0].PinType.Value);
        Assert.Same(element.OutputTypePins[0], list.InputTypePins[0].IncomingPin);
        AssertTheCallsFollowTheSignature();
    }

    [Fact]
    public void RetypingAReturnValueThroughItsTypeNodeMovesTheCallsAndUndoesInOneStep()
    {
        Method.MainReturnNode.AddReturnType();
        _ = new CallMethodNode(caller, SpecifierOfTheMethod());
        var type = new TypeNode(Method, IntType);
        ClassContext.UndoRedo.Clear();

        Assert.True(VmOf(type.OutputTypePins[0]).ConnectTo(VmOf(Method.MainReturnNode.InputTypePins[0])));

        Assert.Equal(IntType, TheCall().ReturnValuePins[0].PinType.Value);
        AssertTheCallsFollowTheSignature();

        ClassContext.UndoRedo.Undo();
        Assert.Equal(TypeSpecifier.FromType<object>(), TheCall().ReturnValuePins[0].PinType.Value);
        Assert.Null(Method.MainReturnNode.InputTypePins[0].IncomingPin);
        Assert.False(ClassContext.UndoRedo.CanUndo);

        ClassContext.UndoRedo.Redo();
        Assert.Equal(IntType, TheCall().ReturnValuePins[0].PinType.Value);
    }

    [Fact]
    public void RemovingAReturnValueMovesTheCallsDropsItsWiresAndUndoesInOneStep()
    {
        Method.MainReturnNode.AddReturnType();
        var sink = new CallMethodNode(caller, ConsoleWriteLine(TypeSpecifier.FromType<object>()));
        GraphUtil.ConnectDataPins(new CallMethodNode(caller, SpecifierOfTheMethod()).ReturnValuePins[0], sink.ArgumentPins[0]);
        ClassContext.UndoRedo.Clear();

        VmOf(Method.MainReturnNode).LeftPinsMinusCommand.Execute(null);

        Assert.Empty(TheCall().ReturnValuePins);
        Assert.Null(sink.ArgumentPins[0].IncomingPin);
        AssertTheCallsFollowTheSignature();

        ClassContext.UndoRedo.Undo();
        Assert.Single(TheCall().ReturnValuePins);
        Assert.Same(TheCall().ReturnValuePins[0], sink.ArgumentPins[0].IncomingPin);
        Assert.False(ClassContext.UndoRedo.CanUndo);
        AssertTheCallsFollowTheSignature();

        ClassContext.UndoRedo.Redo();
        Assert.Empty(TheCall().ReturnValuePins);
    }

    [Fact]
    public void AddingAReturnValueMovesTheCallsAndUndoesInOneStep()
    {
        _ = new CallMethodNode(caller, SpecifierOfTheMethod());

        VmOf(Method.MainReturnNode).LeftPinsPlusCommand.Execute(null);

        Assert.Single(TheCall().ReturnValuePins);
        AssertTheCallsFollowTheSignature();
        ClassContext.UndoRedo.Undo();
        Assert.Empty(TheCall().ReturnValuePins);
        Assert.False(ClassContext.UndoRedo.CanUndo);
        ClassContext.UndoRedo.Redo();
        Assert.Single(TheCall().ReturnValuePins);
    }

    [Fact]
    public void AddingAndRemovingAGenericArgumentMovesTheCallsAndUndoesInOneStep()
    {
        _ = new CallMethodNode(caller, SpecifierOfTheMethod());

        VmOf(Method.MethodEntryNode).RightPinsPlusCommand.Execute(null);

        Assert.Single(TheCall().InputTypePins);
        AssertTheCallsFollowTheSignature();
        ClassContext.UndoRedo.Undo();
        Assert.Empty(TheCall().InputTypePins);
        Assert.False(ClassContext.UndoRedo.CanUndo);
        ClassContext.UndoRedo.Redo();
        Assert.Single(TheCall().InputTypePins);

        VmOf(Method.MethodEntryNode).RightPinsMinusCommand.Execute(null);

        Assert.Empty(TheCall().InputTypePins);
        AssertTheCallsFollowTheSignature();
        ClassContext.UndoRedo.Undo();
        Assert.Single(TheCall().InputTypePins);
    }

    [Fact]
    public void DeletingTheWiredTypeNodeResetsTheParameterToObjectMovesTheCallsAndUndoesInOneStep()
    {
        var type = new TypeNode(Method, IntType);
        Method.MethodEntryNode.AddArgument();
        GraphUtil.ConnectTypePins(type.OutputTypePins[0], Method.MethodEntryNode.InputTypePins[0]);
        _ = new CallMethodNode(caller, SpecifierOfTheMethod());
        Assert.Equal(IntType, TheCall().ArgumentPins[0].PinType.Value);
        ClassContext.UndoRedo.Clear();

        VmOf(type).IsSelected = true;
        Graph.DeleteSelectedNodes();

        Assert.DoesNotContain(type, Method.Nodes);
        Assert.Null(Method.MethodEntryNode.InputTypePins[0].IncomingPin);
        Assert.Equal(TypeSpecifier.FromType<object>(), Method.MethodEntryNode.OutputDataPins[0].PinType.Value);
        Assert.Equal(TypeSpecifier.FromType<object>(), TheCall().ArgumentPins[0].PinType.Value);
        AssertTheCallsFollowTheSignature();

        ClassContext.UndoRedo.Undo();
        Assert.Contains(type, Method.Nodes);
        Assert.Same(type.OutputTypePins[0], Method.MethodEntryNode.InputTypePins[0].IncomingPin);
        Assert.Equal(IntType, Method.MethodEntryNode.OutputDataPins[0].PinType.Value);
        Assert.Equal(IntType, TheCall().ArgumentPins[0].PinType.Value);
        Assert.False(ClassContext.UndoRedo.CanUndo);

        ClassContext.UndoRedo.Redo();
        Assert.Equal(TypeSpecifier.FromType<object>(), TheCall().ArgumentPins[0].PinType.Value);
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
