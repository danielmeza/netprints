using CommunityToolkit.Mvvm.Messaging;
using NetPrints.Core;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Editor.UndoRedo;
using NetPrints.Editor.Variables;
using NetPrints.Graph;

namespace NetPrints.Editor.Tests.Variables;

public class MemberVariableViewModelTests : IDisposable
{
    private readonly ClassContext vm;
    private readonly ClassGraph cls;

    public MemberVariableViewModelTests(TestEditor editor)
    {
        cls = new ClassGraph { Name = "C", Namespace = "N" };
        vm = new ClassContext(cls, editor.Context, new UndoRedoStack());
    }

    public void Dispose() => vm.Dispose();

    [Fact]
    public void CreateVariableUniqueObjectUndoable()
    {
        vm.CreateVariable();
        vm.CreateVariable();

        Assert.Equal(new[] { "Variable", "Variable2" }, vm.Variables.Select(v => v.Name).ToArray());
        Assert.True(vm.Variables.All(v => v.Type == TypeSpecifier.FromType<object>()));

        vm.UndoRedo.Undo();
        Assert.Equal(1, vm.Variables.Count());
    }

    [Fact]
    public void GetterAndSetterAreCreatedWithTypedPinsAndUndoable()
    {
        vm.CreateVariable();
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
        vm.UndoRedo.Undo();
        Assert.Same(setter, variable.Setter);

        variable.RemoveGetterCommand.Execute(null);
        Assert.False(variable.HasGetter);
        vm.UndoRedo.Undo();
        Assert.True(variable.HasGetter);
    }

    [Fact]
    public void OpenGetterSetterAndTypeGraphsSendTheirGraphsToTheContext()
    {
        vm.CreateVariable();
        var variable = vm.Variables.Single();
        variable.AddGetterCommand.Execute(null);
        variable.AddSetterCommand.Execute(null);
        var opened = new List<NodeGraph>();
        vm.Messenger.Register<List<NodeGraph>, OpenGraphMessage>(opened, (list, message) => list.Add(message.Graph));

        variable.OpenGetterCommand.Execute(null);
        variable.OpenSetterCommand.Execute(null);
        variable.OpenTypeGraphCommand.Execute(null);

        Assert.Equal(3, opened.Count);
        Assert.Same(variable.Getter, opened[0]);
        Assert.Same(variable.Setter, opened[1]);
        Assert.Same(variable.Variable.TypeGraph, opened[2]);
    }

    [Fact]
    public void RemoveVariableIsUndoable()
    {
        vm.CreateVariable();
        var variable = vm.Variables.Single();

        variable.RemoveCommand.Execute(null);

        Assert.Empty(vm.Variables);
        vm.UndoRedo.Undo();
        Assert.Same(variable.Variable, vm.Variables.Single().Variable);
    }

    [Fact]
    public void SelectingARowSendsAnInspectorSelectionToTheContext()
    {
        vm.CreateVariable();
        var variable = vm.Variables.Single();
        var selected = new List<MemberVariableViewModel>();
        vm.Messenger.Register<List<MemberVariableViewModel>, SelectInspectorMessage>(selected, (list, message) => list.Add(message.Target));

        variable.SelectCommand.Execute(null);

        Assert.Same(variable, Assert.Single(selected));
    }

    [Fact]
    public void VariableInspectorEditsModelAndAccessorVisibility()
    {
        vm.CreateVariable();
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
        Assert.Contains(nameof(MemberVariableViewModel.Name), changed);
        Assert.Contains(nameof(MemberVariableViewModel.IsStatic), changed);
    }
}
