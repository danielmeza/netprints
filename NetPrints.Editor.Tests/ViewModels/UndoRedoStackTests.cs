using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Editor.Commands;

namespace NetPrints.Editor.Tests.ViewModels;

[TestClass]
public class UndoRedoStackTests
{
    private static ClassGraph NewClass() => new() { Name = "C", Namespace = "N" };

    [TestMethod]
    public void AddAndRemoveVariableArePairs()
    {
        var cls = NewClass();
        var stack = new UndoRedoStack();

        stack.Do(EditorCommands.AddVariable(cls, "Variable"));
        var variable = cls.Variables.Single();
        Assert.AreEqual(TypeSpecifier.FromType<object>(), variable.Type);

        Assert.IsTrue(stack.Undo());
        Assert.IsEmpty(cls.Variables);
        Assert.IsTrue(stack.Redo());
        Assert.AreSame(variable, cls.Variables.Single());

        stack.Do(EditorCommands.RemoveVariable(cls, variable));
        Assert.IsEmpty(cls.Variables);
        stack.Undo();
        Assert.AreSame(variable, cls.Variables.Single(), "undo restores the removed variable");
    }

    [TestMethod]
    public void AddAndRemoveGetterSetterArePairs()
    {
        var cls = NewClass();
        var variable = new Variable(cls, "V", TypeSpecifier.FromType<int>(), null, null, VariableModifiers.None);
        cls.Variables.Add(variable);
        var stack = new UndoRedoStack();

        stack.Do(EditorCommands.AddGetter(variable));
        var getter = variable.GetterMethod;
        Assert.IsNotNull(getter);
        stack.Undo();
        Assert.IsNull(variable.GetterMethod);
        stack.Redo();
        Assert.AreSame(getter, variable.GetterMethod);

        stack.Do(EditorCommands.RemoveGetter(variable));
        Assert.IsNull(variable.GetterMethod);
        stack.Undo();
        Assert.AreSame(getter, variable.GetterMethod);

        stack.Do(EditorCommands.AddSetter(variable));
        var setter = variable.SetterMethod;
        Assert.IsNotNull(setter);
        stack.Do(EditorCommands.RemoveSetter(variable));
        Assert.IsNull(variable.SetterMethod);
        stack.Undo();
        Assert.AreSame(setter, variable.SetterMethod);
        stack.Undo();
        Assert.IsNull(variable.SetterMethod);
    }

    [TestMethod]
    public void OverloadChangeRestoresPreviousOverload()
    {
        var cls = NewClass();
        var method = new MethodGraph("M") { Class = cls };
        cls.Methods.Add(method);
        var makeArray = new MakeArrayNode(method);
        var stack = new UndoRedoStack();

        Assert.IsFalse(makeArray.UsePredefinedSize);
        stack.Do(EditorCommands.ChangeOverload(makeArray, ModelOperations.UsePredefinedSize));
        Assert.IsTrue(makeArray.UsePredefinedSize);
        stack.Undo();
        Assert.IsFalse(makeArray.UsePredefinedSize);

        var stringType = TypeSpecifier.FromType<string>();
        var writeString = new MethodSpecifier("WriteLine", [new MethodParameter("value", stringType, MethodParameterPassType.Default, false, null)],
            [], MethodModifiers.Static, MemberVisibility.Public, TypeSpecifier.FromType(typeof(Console)), []);
        var writeInt = new MethodSpecifier("WriteLine", [new MethodParameter("value", TypeSpecifier.FromType<int>(), MethodParameterPassType.Default, false, null)],
            [], MethodModifiers.Static, MemberVisibility.Public, TypeSpecifier.FromType(typeof(Console)), []);
        var call = new CallMethodNode(method, writeString);
        GraphUtil.ConnectExecPins(method.EntryNode.InitialExecutionPin, call.InputExecPins[0]);

        stack.Do(EditorCommands.ChangeOverload(call, writeInt));
        var replaced = method.Nodes.OfType<CallMethodNode>().Single();
        Assert.AreEqual(writeInt, replaced.MethodSpecifier);
        Assert.AreSame(replaced.InputExecPins[0], method.EntryNode.InitialExecutionPin.OutgoingPin, "exec connections are restored");

        stack.Undo();
        var restored = method.Nodes.OfType<CallMethodNode>().Single();
        Assert.AreEqual(writeString, restored.MethodSpecifier);
        Assert.AreSame(restored.InputExecPins[0], method.EntryNode.InitialExecutionPin.OutgoingPin);
    }

    [TestMethod]
    public void RemoveMethodHasNoOpUndo()
    {
        var cls = NewClass();
        var method = new MethodGraph("M") { Class = cls };
        cls.Methods.Add(method);
        var stack = new UndoRedoStack();

        stack.Do(EditorCommands.RemoveMethod(cls, method));
        Assert.IsEmpty(cls.Methods);
        Assert.IsTrue(stack.Undo());
        Assert.IsEmpty(cls.Methods, "undo of remove method does nothing (as in the WPF editor)");
    }

    [TestMethod]
    public void NewActionClearsRedo()
    {
        var cls = NewClass();
        var stack = new UndoRedoStack();

        stack.Do(EditorCommands.AddVariable(cls, "A"));
        stack.Undo();
        Assert.IsTrue(stack.CanRedo);

        stack.Do(EditorCommands.AddVariable(cls, "B"));
        Assert.IsFalse(stack.CanRedo);
        Assert.IsFalse(stack.Redo());
        CollectionAssert.AreEqual(new[] { "B" }, cls.Variables.Select(v => v.Name).ToArray());
    }
}
