using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Editor.UndoRedo;

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
}
