using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Editor.Tests.Fakes;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.Variables;

namespace NetPrints.Editor.Tests.ViewModels;

[TestClass]
public class MemberVariableVMTests
{
    private ClassEditorVM vm = null!;
    private ClassGraph cls = null!;

    [TestInitialize]
    public void Setup()
    {
        cls = new ClassGraph { Name = "C", Namespace = "N" };
        vm = new ClassEditorVM(cls, new TestEditor().Context);
    }

    [TestCleanup]
    public void Cleanup() => vm.Dispose();

    [TestMethod]
    public void CreateVariableUniqueObjectUndoable()
    {
        vm.CreateVariableCommand.Execute(null);
        vm.CreateVariableCommand.Execute(null);

        CollectionAssert.AreEqual(new[] { "Variable", "Variable2" }, vm.Variables.Select(v => v.Name).ToArray());
        Assert.IsTrue(vm.Variables.All(v => v.Type == TypeSpecifier.FromType<object>()));

        vm.UndoCommand.Execute(null);
        Assert.HasCount(1, vm.Variables);
    }

    [TestMethod]
    public void GetterAndSetterAreCreatedWithTypedPinsAndUndoable()
    {
        vm.CreateVariableCommand.Execute(null);
        var variable = vm.Variables.Single();

        variable.AddGetterCommand.Execute(null);
        Assert.IsTrue(variable.HasGetter);
        var getter = variable.Getter!;
        Assert.AreEqual("get_Variable", getter.Name);
        Assert.AreSame(getter.MainReturnNode.ReturnPin, getter.EntryNode.InitialExecutionPin.OutgoingPin);
        Assert.IsNotNull(getter.MainReturnNode.InputTypePins[0].IncomingPin, "return type pin is connected");
        Assert.AreEqual(560, getter.EntryNode.PositionX);

        variable.AddSetterCommand.Execute(null);
        var setter = variable.Setter!;
        Assert.AreEqual("set_Variable", setter.Name);
        Assert.IsNotNull(setter.EntryNode.InputTypePins[0].IncomingPin, "argument type pin is connected");

        variable.RemoveSetterCommand.Execute(null);
        Assert.IsFalse(variable.HasSetter);
        vm.UndoCommand.Execute(null);
        Assert.AreSame(setter, variable.Setter);

        variable.RemoveGetterCommand.Execute(null);
        Assert.IsFalse(variable.HasGetter);
        vm.UndoCommand.Execute(null);
        Assert.IsTrue(variable.HasGetter);
    }

    [TestMethod]
    public void OpenGetterSetterAndTypeGraphs()
    {
        vm.CreateVariableCommand.Execute(null);
        var variable = vm.Variables.Single();
        variable.AddGetterCommand.Execute(null);
        variable.AddSetterCommand.Execute(null);

        variable.OpenGetterCommand.Execute(null);
        Assert.AreSame(variable.Getter, vm.OpenedGraph?.Graph);
        variable.OpenSetterCommand.Execute(null);
        Assert.AreSame(variable.Setter, vm.OpenedGraph?.Graph);
        variable.OpenTypeGraphCommand.Execute(null);
        Assert.AreSame(variable.Variable.TypeGraph, vm.OpenedGraph?.Graph);

        // Removing the getter closes its graph.
        variable.OpenGetterCommand.Execute(null);
        variable.RemoveGetterCommand.Execute(null);
        Assert.IsNull(vm.OpenedGraph);
    }

    [TestMethod]
    public void RemoveVariableIsUndoableAndClearsInspector()
    {
        vm.CreateVariableCommand.Execute(null);
        var variable = vm.Variables.Single();
        variable.SelectCommand.Execute(null);

        variable.RemoveCommand.Execute(null);

        Assert.IsEmpty(vm.Variables);
        Assert.AreEqual(InspectorKind.Class, vm.Inspector);
        vm.UndoCommand.Execute(null);
        Assert.AreSame(variable.Variable, vm.Variables.Single().Variable);
    }

    [TestMethod]
    public void VariableInspectorEditsModelAndAccessorVisibility()
    {
        vm.CreateVariableCommand.Execute(null);
        var variable = vm.Variables.Single();
        variable.AddGetterCommand.Execute(null);

        var changed = new List<string?>();
        variable.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        variable.Name = "Renamed";
        variable.IsStatic = true;
        variable.IsReadOnly = true;
        variable.Visibility = MemberVisibility.Public;

        Assert.AreEqual("Renamed", variable.Variable.Name);
        Assert.AreEqual(VariableModifiers.Static | VariableModifiers.ReadOnly, variable.Variable.Modifiers);
        Assert.AreEqual(MemberVisibility.Public, variable.Getter!.Visibility, "accessor with the same visibility follows");
        CollectionAssert.Contains(changed, nameof(MemberVariableVM.Name));
        CollectionAssert.Contains(changed, nameof(MemberVariableVM.IsStatic));
    }
}
