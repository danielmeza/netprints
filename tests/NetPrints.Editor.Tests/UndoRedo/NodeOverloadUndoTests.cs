using NetPrints.Core;
using NetPrints.Editor.UndoRedo;
using NetPrints.Graph;

namespace NetPrints.Editor.Tests.UndoRedo;

public class NodeOverloadUndoTests
{
    private static MethodSpecifier WriteLine<T>() => new("WriteLine", [new MethodParameter("value", TypeSpecifier.FromType<T>(), MethodParameterPassType.Default, false, null)],
        [], MethodModifiers.Static, MemberVisibility.Public, TypeSpecifier.FromType(typeof(Console)), []);

    private static MethodGraph NewMethod()
    {
        var cls = new ClassGraph { Name = "C", Namespace = "N" };
        var method = new MethodGraph("M") { Class = cls };
        cls.Methods.Add(method);
        return method;
    }

    [Fact]
    public void RedoingADeleteAfterAnOverloadChangeRemovesTheCurrentNode()
    {
        MethodGraph method = NewMethod();
        var call = new CallMethodNode(method, WriteLine<string>());
        var stack = new UndoRedoStack();
        stack.Do(EditorCommands.ChangeOverload(call, WriteLine<int>()));
        var replaced = method.Nodes.OfType<CallMethodNode>().Single();
        stack.Do(EditorCommands.RemoveNodes([replaced]));
        stack.Undo();
        stack.Undo();
        stack.Redo();
        stack.Redo();

        Assert.Empty(method.Nodes.OfType<CallMethodNode>());
        stack.Undo();
        Assert.Single(method.Nodes.OfType<CallMethodNode>());
    }

    [Fact]
    public void UndoingADeleteAfterANeighboursOverloadChangeConnectsToTheCurrentNeighbour()
    {
        MethodGraph method = NewMethod();
        var y = new CallMethodNode(method, WriteLine<string>());
        var a = new CallMethodNode(method, WriteLine<string>());
        GraphUtil.ConnectExecPins(method.EntryNode.InitialExecutionPin, y.InputExecPins[0]);
        GraphUtil.ConnectExecPins(y.OutputExecPins[0], a.InputExecPins[0]);
        var stack = new UndoRedoStack();
        stack.Do(EditorCommands.RemoveNodes([a]));
        stack.Do(EditorCommands.ChangeOverload(y, WriteLine<int>()));
        stack.Undo();
        stack.Undo();

        Assert.Contains(a, method.Nodes);
        Assert.All(a.InputExecPins[0].IncomingPins, pin => Assert.Contains(pin.Node, method.Nodes));
    }

    [Fact]
    public void UndoingAnOverloadChangeAfterAddingTheNodeLeavesNoNodeAndNothingToUndo()
    {
        MethodGraph method = NewMethod();
        var call = new CallMethodNode(method, WriteLine<string>());
        var stack = new UndoRedoStack();
        stack.Record(EditorCommands.AddNode(call));
        stack.Do(EditorCommands.ChangeOverload(call, WriteLine<int>()));
        stack.Undo();
        stack.Undo();

        Assert.Empty(method.Nodes.OfType<CallMethodNode>());
        Assert.False(stack.CanUndo);
        stack.Redo();
        stack.Redo();
        Assert.Single(method.Nodes.OfType<CallMethodNode>());
    }
}
