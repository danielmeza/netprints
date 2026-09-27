using NetPrints.Core;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.Graph;
using NetPrints.Editor.ModelSync;
using NetPrints.Editor.Search;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Editor.Variables;
using NetPrints.Graph;

namespace NetPrints.Editor.Tests.Variables;

/// <summary>US5, sub-phase H: the Variables panel's "Method" group (T087/T088).</summary>
public class LocalVariableTests : IDisposable
{
    private readonly TestEditor editor;
    private readonly ClassEditorVM vm;
    private readonly MethodGraph method;
    private readonly NodeGraphVM graph;

    public LocalVariableTests(TestEditor editor)
    {
        this.editor = editor;
        var cls = new ClassGraph { Name = "C", Namespace = "N" };
        vm = new ClassEditorVM(cls, editor.Context);
        vm.CreateMethodCommand.Execute(null); // also opens it (mirrors EventGraphTests' Create Event Graph)
        graph = vm.OpenedGraph ?? throw new InvalidOperationException("CreateMethodCommand did not open the new method.");
        method = (MethodGraph)graph.Graph;
    }

    public void Dispose() => vm.Dispose();

    private ObservableViewModelCollection<LocalVariableVM, LocalVariable> MethodVariables =>
        vm.VariablesPanel.MethodVariables ?? throw new InvalidOperationException("No method or constructor graph is open.");

    private LocalVariableVM CreateLocal()
    {
        vm.VariablesPanel.CreateLocalVariableCommand.Execute(null);
        return MethodVariables.Single();
    }

    [Fact]
    public void CreateLocalVariableUniqueObjectUndoable()
    {
        vm.VariablesPanel.CreateLocalVariableCommand.Execute(null);
        vm.VariablesPanel.CreateLocalVariableCommand.Execute(null);

        Assert.Equal(["Local", "Local2"], method.LocalVariables.Select(l => l.Name).ToArray());
        Assert.True(method.LocalVariables.All(l => l.Type == TypeSpecifier.FromType<object>()));

        vm.UndoCommand.Execute(null);
        Assert.Equal(1, method.LocalVariables.Count);
    }

    [Fact]
    public void RenameLocalVariableRetargetsExistingNodesUndoable()
    {
        var local = CreateLocal();
        var position = new GraphPoint(100, 100);
        graph.GetSetChooser.Open(local.Specifier, position);
        graph.GetSetChooser.GetCommand.Execute(null);
        var getter = method.Nodes.OfType<VariableGetterNode>().Single();

        local.Name = "Renamed";

        Assert.Equal("Renamed", getter.VariableName); // same node instance, retargeted in place
        Assert.Same(local.Local, method.LocalVariables.Single());
        Assert.Equal("Renamed", local.Local.Name);

        vm.UndoCommand.Execute(null);
        Assert.Equal("Local", local.Local.Name);
        Assert.Equal("Local", getter.VariableName);
        Assert.Same(getter, method.Nodes.OfType<VariableGetterNode>().Single()); // not replaced
    }

    [Fact]
    public void RenameRejectsDuplicateOrInvalidNamesAndDoesNotUndo()
    {
        CreateLocal(); // "Local"
        vm.VariablesPanel.CreateLocalVariableCommand.Execute(null); // "Local2"
        var second = MethodVariables[MethodVariables.Count - 1];

        second.Name = "Local"; // taken by the first local
        Assert.Equal("Local2", second.Name);

        second.Name = "class"; // a reserved keyword, not a valid identifier
        Assert.Equal("Local2", second.Name);

        // Exactly the two creates are undoable; neither rejected rename pushed a command.
        vm.UndoCommand.Execute(null);
        vm.UndoCommand.Execute(null);
        Assert.Empty(method.LocalVariables);
        Assert.False(vm.UndoRedo.CanUndo);
    }

    [Fact]
    public async Task RetypeLocalVariableReplacesExistingNodesUndoable()
    {
        var local = CreateLocal();
        var position = new GraphPoint(100, 100);
        graph.GetSetChooser.Open(local.Specifier, position);
        graph.GetSetChooser.SetCommand.Execute(null);
        var setter = method.Nodes.OfType<VariableSetterNode>().Single();
        setter.PositionX = 42;
        setter.PositionY = 24;
        GraphUtil.ConnectExecPins(method.EntryNode.InitialExecutionPin, setter.InputExecPins[0]);
        GraphUtil.ConnectExecPins(setter.OutputExecPins[0], method.MainReturnNode.ReturnPin);

        editor.Dialogs.TypeAnswer = TypeSpecifier.FromType<int>();
        await local.RetypeCommand.ExecuteAsync(null);

        var replacement = method.Nodes.OfType<VariableSetterNode>().Single();
        Assert.NotSame(setter, replacement); // a retype changes the pin shape, so the node is replaced
        Assert.Equal(TypeSpecifier.FromType<int>(), replacement.Variable.Type);
        Assert.Equal(42, replacement.PositionX);
        Assert.Equal(24, replacement.PositionY);
        Assert.Same(replacement.InputExecPins[0], method.EntryNode.InitialExecutionPin.OutgoingPin);
        Assert.Same(method.MainReturnNode.ReturnPin, replacement.OutputExecPins[0].OutgoingPin);

        vm.UndoCommand.Execute(null);
        var restored = method.Nodes.OfType<VariableSetterNode>().Single();
        Assert.Equal(TypeSpecifier.FromType<object>(), restored.Variable.Type);
        Assert.Same(restored.InputExecPins[0], method.EntryNode.InitialExecutionPin.OutgoingPin);
    }

    [Fact]
    public void RemoveLocalVariableRemovesAndRestoresNodesUndoably()
    {
        var local = CreateLocal();
        var position = new GraphPoint(100, 100);
        graph.GetSetChooser.Open(local.Specifier, position);
        graph.GetSetChooser.SetCommand.Execute(null);
        var setter = method.Nodes.OfType<VariableSetterNode>().Single();
        GraphUtil.ConnectExecPins(method.EntryNode.InitialExecutionPin, setter.InputExecPins[0]);
        GraphUtil.ConnectExecPins(setter.OutputExecPins[0], method.MainReturnNode.ReturnPin);

        local.RemoveCommand.Execute(null);

        Assert.Empty(method.LocalVariables);
        Assert.Empty(method.Nodes.OfType<VariableSetterNode>());
        Assert.Null(method.EntryNode.InitialExecutionPin.OutgoingPin);

        vm.UndoCommand.Execute(null);

        Assert.Same(local.Local, method.LocalVariables.Single());
        var restored = method.Nodes.OfType<VariableSetterNode>().Single();
        Assert.Same(setter, restored); // undo restores the same instance, not a rebuilt one
        Assert.Same(restored.InputExecPins[0], method.EntryNode.InitialExecutionPin.OutgoingPin);
        Assert.Same(method.MainReturnNode.ReturnPin, restored.OutputExecPins[0].OutgoingPin);
    }

    [Fact]
    public void DroppingALocalOpensGetSetChooserWithBothEnabled()
    {
        // H1's stopgap (VariableSpecifier.DeclaringType is null => CanGet/CanSet both true) is now
        // reachable through the panel's drag & drop (NodeGraphVM.Drop(LocalVariableVM, GraphPoint)).
        var local = CreateLocal();
        var position = new GraphPoint(50, 60);

        graph.Drop(local, position);

        var opened = graph.GetSetChooser.Variable ?? throw new InvalidOperationException("Expected the chooser to have opened with a variable.");
        Assert.True(graph.GetSetChooser.IsOpen);
        Assert.True(graph.GetSetChooser.CanGet);
        Assert.True(graph.GetSetChooser.CanSet);
        Assert.Equal(VariableScope.Local, opened.Scope);
        Assert.Null(opened.DeclaringType);
    }

    [Fact]
    public void SetterNewValuePinIsAtIndexZeroForALocal()
    {
        // H1's fix to VariableSetterNode.NewValuePin (IsStatic || IsLocalVariable ? [0] : [1]),
        // exercised through the editor's own drop-to-Set flow rather than a direct constructor call.
        var local = CreateLocal();
        graph.GetSetChooser.Open(local.Specifier, new GraphPoint(0, 0));
        graph.GetSetChooser.SetCommand.Execute(null);

        var setter = method.Nodes.OfType<VariableSetterNode>().Single();

        Assert.Single(setter.InputDataPins);
        Assert.Same(setter.InputDataPins[0], setter.NewValuePin);
        Assert.Null(setter.TargetPin);
    }

    [Fact]
    public void VariablesPanelBuildsMethodGroupFromOpenedGraphAndClearsWhenClosed()
    {
        Assert.True(vm.VariablesPanel.HasMethodGroup);
        Assert.Equal("Method: Method", vm.VariablesPanel.MethodGroupHeader);
        Assert.Same(vm.Variables, vm.VariablesPanel.ClassVariables);

        vm.OpenedGraph = null;

        Assert.False(vm.VariablesPanel.HasMethodGroup);
        Assert.Null(vm.VariablesPanel.MethodVariables);
        Assert.Equal("", vm.VariablesPanel.MethodGroupHeader);
    }

    [Fact]
    public void MethodVariablesSearchCategoryListsLocals()
    {
        CreateLocal();
        using var search = new SuggestionListVM(graph);

        var rows = search.BuildItems(null);

        Assert.Contains(rows, r => r.IsHeader && r.Category == "Method Variables");
        Assert.Contains(rows, r => !r.IsHeader && r.Category == "Method Variables"
            && r.Value is VariableSpecifier v && v.Name == "Local" && v.Scope == VariableScope.Local);
    }
}
