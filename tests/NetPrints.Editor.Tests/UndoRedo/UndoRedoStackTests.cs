using NetPrints.Core;
using NetPrints.Editor.UndoRedo;
using NetPrints.Graph;

namespace NetPrints.Editor.Tests.UndoRedo;

public class UndoRedoStackTests
{
    private static ClassGraph NewClass() => new() { Name = "C", Namespace = "N" };

    [Fact]
    public void AddAndRemoveVariableArePairs()
    {
        var cls = NewClass();
        var stack = new UndoRedoStack();

        stack.Do(EditorCommands.AddVariable(cls, "Variable"));
        var variable = cls.Variables.Single();
        Assert.Equal(TypeSpecifier.FromType<object>(), variable.Type);

        Assert.True(stack.Undo());
        Assert.Empty(cls.Variables);
        Assert.True(stack.Redo());
        Assert.Same(variable, cls.Variables.Single());

        stack.Do(EditorCommands.RemoveVariable(cls, variable));
        Assert.Empty(cls.Variables);
        stack.Undo();
        Assert.Same(variable, cls.Variables.Single());
    }

    [Fact]
    public void AddAndRemoveGetterSetterArePairs()
    {
        var cls = NewClass();
        var variable = new Variable(cls, "V", TypeSpecifier.FromType<int>(), null, null, VariableModifiers.None);
        cls.Variables.Add(variable);
        var stack = new UndoRedoStack();

        stack.Do(EditorCommands.AddGetter(variable));
        var getter = variable.GetterMethod;
        Assert.NotNull(getter);
        stack.Undo();
        Assert.Null(variable.GetterMethod);
        stack.Redo();
        Assert.Same(getter, variable.GetterMethod);

        stack.Do(EditorCommands.RemoveGetter(variable));
        Assert.Null(variable.GetterMethod);
        stack.Undo();
        Assert.Same(getter, variable.GetterMethod);

        stack.Do(EditorCommands.AddSetter(variable));
        var setter = variable.SetterMethod;
        Assert.NotNull(setter);
        stack.Do(EditorCommands.RemoveSetter(variable));
        Assert.Null(variable.SetterMethod);
        stack.Undo();
        Assert.Same(setter, variable.SetterMethod);
        stack.Undo();
        Assert.Null(variable.SetterMethod);
    }

    [Fact]
    public void OverloadChangeRestoresPreviousOverload()
    {
        var cls = NewClass();
        var method = new MethodGraph("M") { Class = cls };
        cls.Methods.Add(method);
        var makeArray = new MakeArrayNode(method);
        var stack = new UndoRedoStack();

        Assert.False(makeArray.UsePredefinedSize);
        stack.Do(EditorCommands.ChangeOverload(makeArray, ModelOperations.UsePredefinedSize));
        Assert.True(makeArray.UsePredefinedSize);
        stack.Undo();
        Assert.False(makeArray.UsePredefinedSize);

        var stringType = TypeSpecifier.FromType<string>();
        var writeString = new MethodSpecifier("WriteLine", [new MethodParameter("value", stringType, MethodParameterPassType.Default, false, null)],
            [], MethodModifiers.Static, MemberVisibility.Public, TypeSpecifier.FromType(typeof(Console)), []);
        var writeInt = new MethodSpecifier("WriteLine", [new MethodParameter("value", TypeSpecifier.FromType<int>(), MethodParameterPassType.Default, false, null)],
            [], MethodModifiers.Static, MemberVisibility.Public, TypeSpecifier.FromType(typeof(Console)), []);
        var call = new CallMethodNode(method, writeString);
        GraphUtil.ConnectExecPins(method.EntryNode.InitialExecutionPin, call.InputExecPins[0]);

        stack.Do(EditorCommands.ChangeOverload(call, writeInt));
        var replaced = method.Nodes.OfType<CallMethodNode>().Single();
        Assert.Equal(writeInt, replaced.MethodSpecifier);
        Assert.Same(replaced.InputExecPins[0], method.EntryNode.InitialExecutionPin.OutgoingPin);

        stack.Undo();
        var restored = method.Nodes.OfType<CallMethodNode>().Single();
        Assert.Equal(writeString, restored.MethodSpecifier);
        Assert.Same(restored.InputExecPins[0], method.EntryNode.InitialExecutionPin.OutgoingPin);
    }

    [Fact]
    public void RemoveMethodHasNoOpUndo()
    {
        var cls = NewClass();
        var method = new MethodGraph("M") { Class = cls };
        cls.Methods.Add(method);
        var stack = new UndoRedoStack();

        stack.Do(EditorCommands.RemoveMethod(cls, method));
        Assert.Empty(cls.Methods);
        Assert.True(stack.Undo());
        Assert.Empty(cls.Methods);
    }

    [Fact]
    public void NewActionClearsRedo()
    {
        var cls = NewClass();
        var stack = new UndoRedoStack();

        stack.Do(EditorCommands.AddVariable(cls, "A"));
        stack.Undo();
        Assert.True(stack.CanRedo);

        stack.Do(EditorCommands.AddVariable(cls, "B"));
        Assert.False(stack.CanRedo);
        Assert.False(stack.Redo());
        Assert.Equal(new[] { "B" }, cls.Variables.Select(v => v.Name).ToArray());
    }

    [Fact]
    public void ChainedOverloadChangesUndoAndRedoInOrder()
    {
        // PAR-60: two overload changes of the same node, undone and redone step by step.
        var cls = NewClass();
        var method = new MethodGraph("M") { Class = cls };
        cls.Methods.Add(method);
        var console = TypeSpecifier.FromType(typeof(Console));
        MethodSpecifier WriteLine(params TypeSpecifier[] parameters) =>
            new("WriteLine", parameters.Select(p => new MethodParameter("value", p, MethodParameterPassType.Default, false, null)),
                [], MethodModifiers.Static, MemberVisibility.Public, console, []);
        var writeString = WriteLine(TypeSpecifier.FromType<string>());
        var writeInt = WriteLine(TypeSpecifier.FromType<int>());
        var writeNothing = WriteLine();
        var call = new CallMethodNode(method, writeString);
        GraphUtil.ConnectExecPins(method.EntryNode.InitialExecutionPin, call.InputExecPins[0]);
        var stack = new UndoRedoStack();

        CallMethodNode Current()
        {
            var node = Assert.Single(method.Nodes.OfType<CallMethodNode>());
            Assert.Same(node.InputExecPins[0], method.EntryNode.InitialExecutionPin.OutgoingPin);
            return node;
        }

        stack.Do(EditorCommands.ChangeOverload(Current(), writeInt));
        stack.Do(EditorCommands.ChangeOverload(Current(), writeNothing));
        Assert.Equal(writeNothing, Current().MethodSpecifier);

        stack.Undo();
        Assert.Equal(writeInt, Current().MethodSpecifier);
        stack.Undo();
        Assert.Equal(writeString, Current().MethodSpecifier);

        stack.Redo();
        Assert.Equal(writeInt, Current().MethodSpecifier);
        stack.Redo();
        Assert.Equal(writeNothing, Current().MethodSpecifier);

        stack.Undo();
        stack.Undo();
        Assert.Equal(writeString, Current().MethodSpecifier);
    }

    [Fact]
    public void MultiStepUndoRedoAcrossCommandKinds()
    {
        // PAR-60: a mixed history is undone in reverse order and redone in order.
        var cls = NewClass();
        var stack = new UndoRedoStack();

        stack.Do(EditorCommands.AddVariable(cls, "A"));
        var a = cls.Variables.Single();
        stack.Do(EditorCommands.AddGetter(a));
        stack.Do(EditorCommands.AddSetter(a));
        stack.Do(EditorCommands.AddVariable(cls, "B"));
        stack.Do(EditorCommands.RemoveGetter(a));

        Assert.Null(a.GetterMethod);
        stack.Undo(); // getter back
        Assert.NotNull(a.GetterMethod);
        stack.Undo(); // B removed
        Assert.Equal(["A"], cls.Variables.Select(v => v.Name));
        stack.Undo(); // setter removed
        Assert.Null(a.SetterMethod);
        stack.Undo(); // getter removed
        Assert.Null(a.GetterMethod);
        stack.Undo(); // A removed
        Assert.Empty(cls.Variables);
        Assert.False(stack.Undo());

        for (int i = 0; i < 5; i++)
        {
            Assert.True(stack.Redo());
        }

        Assert.Equal(["A", "B"], cls.Variables.Select(v => v.Name));
        Assert.Same(a, cls.Variables[0]);
        Assert.Null(a.GetterMethod);
        Assert.NotNull(a.SetterMethod);
        Assert.False(stack.Redo());
    }
}
