using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.Variables;

namespace NetPrints.Editor.Tests.Variables;

public class MemberVariableVMTests : IDisposable
{
    private readonly ClassEditorVM vm;
    private readonly ClassGraph cls;

    public MemberVariableVMTests(TestEditor editor)
    {
        cls = new ClassGraph { Name = "C", Namespace = "N" };
        vm = new ClassEditorVM(cls, editor.Context);
    }

    public void Dispose() => vm.Dispose();

    [Fact]
    public void CreateVariableUniqueObjectUndoable()
    {
        vm.CreateVariableCommand.Execute(null);
        vm.CreateVariableCommand.Execute(null);

        Assert.Equal(new[] { "Variable", "Variable2" }, vm.Variables.Select(v => v.Name).ToArray());
        Assert.True(vm.Variables.All(v => v.Type == TypeSpecifier.FromType<object>()));

        vm.UndoCommand.Execute(null);
        Assert.Equal(1, vm.Variables.Count());
    }

    [Fact]
    public void GetterAndSetterAreCreatedWithTypedPinsAndUndoable()
    {
        vm.CreateVariableCommand.Execute(null);
        var variable = vm.Variables.Single();

        variable.AddGetterCommand.Execute(null);
        Assert.True(variable.HasGetter);
        var getter = variable.Getter!;
        Assert.Equal("get_Variable", getter.Name);
        Assert.Same(getter.MainReturnNode.ReturnPin, getter.EntryNode.InitialExecutionPin.OutgoingPin);
        Assert.NotNull(getter.MainReturnNode.InputTypePins[0].IncomingPin);
        Assert.Equal(560, getter.EntryNode.PositionX);

        variable.AddSetterCommand.Execute(null);
        var setter = variable.Setter!;
        Assert.Equal("set_Variable", setter.Name);
        Assert.NotNull(setter.EntryNode.InputTypePins[0].IncomingPin);

        variable.RemoveSetterCommand.Execute(null);
        Assert.False(variable.HasSetter);
        vm.UndoCommand.Execute(null);
        Assert.Same(setter, variable.Setter);

        variable.RemoveGetterCommand.Execute(null);
        Assert.False(variable.HasGetter);
        vm.UndoCommand.Execute(null);
        Assert.True(variable.HasGetter);
    }

    [Fact]
    public void OpenGetterSetterAndTypeGraphs()
    {
        vm.CreateVariableCommand.Execute(null);
        var variable = vm.Variables.Single();
        variable.AddGetterCommand.Execute(null);
        variable.AddSetterCommand.Execute(null);

        variable.OpenGetterCommand.Execute(null);
        Assert.Same(variable.Getter, vm.OpenedGraph?.Graph);
        variable.OpenSetterCommand.Execute(null);
        Assert.Same(variable.Setter, vm.OpenedGraph?.Graph);
        variable.OpenTypeGraphCommand.Execute(null);
        Assert.Same(variable.Variable.TypeGraph, vm.OpenedGraph?.Graph);

        // Removing the getter closes its graph.
        variable.OpenGetterCommand.Execute(null);
        variable.RemoveGetterCommand.Execute(null);
        Assert.Null(vm.OpenedGraph);
    }

    [Fact]
    public void RemoveVariableIsUndoableAndClearsInspector()
    {
        vm.CreateVariableCommand.Execute(null);
        var variable = vm.Variables.Single();
        variable.SelectCommand.Execute(null);

        variable.RemoveCommand.Execute(null);

        Assert.Empty(vm.Variables);
        Assert.Equal(InspectorKind.Class, vm.Inspector);
        vm.UndoCommand.Execute(null);
        Assert.Same(variable.Variable, vm.Variables.Single().Variable);
    }

    [Fact]
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

        Assert.Equal("Renamed", variable.Variable.Name);
        Assert.Equal(VariableModifiers.Static | VariableModifiers.ReadOnly, variable.Variable.Modifiers);
        Assert.Equal(MemberVisibility.Public, variable.Getter!.Visibility);
        Assert.Contains(nameof(MemberVariableVM.Name), changed);
        Assert.Contains(nameof(MemberVariableVM.IsStatic), changed);
    }

    [Fact]
    public void UndoOfAddVariableClearsItsInspector()
    {
        vm.CreateVariableCommand.Execute(null);
        vm.Variables.Single().SelectCommand.Execute(null);
        Assert.True(vm.ShowVariableInspector);

        vm.UndoCommand.Execute(null);

        Assert.Empty(cls.Variables);
        Assert.Null(vm.SelectedVariable);
        Assert.False(vm.ShowVariableInspector);
        Assert.Equal(InspectorKind.Class, vm.Inspector);
    }

    [Fact]
    public void UndoOfAddVariableClosesItsGraphs()
    {
        vm.CreateVariableCommand.Execute(null);
        vm.Variables.Single().OpenTypeGraphCommand.Execute(null);
        Assert.NotNull(vm.OpenedGraph);

        vm.UndoCommand.Execute(null);

        Assert.Null(vm.OpenedGraph);
    }

    [Fact]
    public void UndoOfAddGetterOrSetterClosesTheAccessorGraph()
    {
        vm.CreateVariableCommand.Execute(null);
        var variable = vm.Variables.Single();

        variable.AddGetterCommand.Execute(null);
        variable.OpenGetterCommand.Execute(null);
        Assert.Same(variable.Getter, vm.OpenedGraph?.Graph);
        vm.UndoCommand.Execute(null);
        Assert.Null(variable.Getter);
        Assert.Null(vm.OpenedGraph);

        variable.AddSetterCommand.Execute(null);
        variable.OpenSetterCommand.Execute(null);
        Assert.Same(variable.Setter, vm.OpenedGraph?.Graph);
        vm.UndoCommand.Execute(null);
        Assert.Null(variable.Setter);
        Assert.Null(vm.OpenedGraph);
    }
}
