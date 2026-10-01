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
    private readonly ClassEditorViewModel vm;
    private readonly MethodGraph method;
    private readonly NodeGraphViewModel graph;

    public LocalVariableTests(TestEditor editor)
    {
        this.editor = editor;
        var cls = new ClassGraph { Name = "C", Namespace = "N" };
        vm = new ClassEditorViewModel(cls, editor.Context);
        vm.CreateMethodCommand.Execute(null); // also opens it (mirrors EventGraphTests' Create Event Graph)
        graph = vm.OpenedGraph ?? throw new InvalidOperationException("CreateMethodCommand did not open the new method.");
        method = (MethodGraph)graph.Graph;
    }

    public void Dispose() => vm.Dispose();

    private ObservableViewModelCollection<LocalVariableViewModel, LocalVariable> MethodVariables =>
        vm.VariablesPanel.MethodVariables ?? throw new InvalidOperationException("No method or constructor graph is open.");

    private LocalVariableViewModel CreateLocal()
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
        var literal = new LiteralNode(method, TypeSpecifier.FromType<object>());
        GraphUtil.ConnectDataPins(literal.ValuePin, setter.NewValuePin);

        editor.Dialogs.TypeAnswer = TypeSpecifier.FromType<int>();
        await local.RetypeCommand.ExecuteAsync(null);

        var replacement = method.Nodes.OfType<VariableSetterNode>().Single();
        Assert.NotSame(setter, replacement); // a retype changes the pin shape, so the node is replaced
        Assert.Equal(TypeSpecifier.FromType<int>(), replacement.Variable.Type);
        Assert.Equal(42, replacement.PositionX);
        Assert.Equal(24, replacement.PositionY);
        Assert.Same(replacement.InputExecPins[0], method.EntryNode.InitialExecutionPin.OutgoingPin);
        Assert.Same(method.MainReturnNode.ReturnPin, replacement.OutputExecPins[0].OutgoingPin);
        Assert.Null(replacement.NewValuePin.IncomingPin); // data connections are not carried to the new-type node

        vm.UndoCommand.Execute(null);
        var restored = method.Nodes.OfType<VariableSetterNode>().Single();
        Assert.Same(setter, restored); // R2-04: undo restores the exact original instance, not a rebuilt one
        Assert.Equal(TypeSpecifier.FromType<object>(), restored.Variable.Type);
        Assert.Same(restored.InputExecPins[0], method.EntryNode.InitialExecutionPin.OutgoingPin);
        Assert.Same(literal.ValuePin, restored.NewValuePin.IncomingPin); // R2-04: the data wire is back too
    }

    [Fact]
    public async Task RetypeLocalVariableRestoresDataConnectionsAndIdsAcrossUndoRedoCycles()
    {
        var local = CreateLocal();
        var position = new GraphPoint(100, 100);

        // A getter wired into a call.
        graph.GetSetChooser.Open(local.Specifier, position);
        graph.GetSetChooser.GetCommand.Execute(null);
        var getter = method.Nodes.OfType<VariableGetterNode>().Single();
        getter.PositionX = 10;
        getter.PositionY = 20;
        var sink = new MethodSpecifier("Sink", [new MethodParameter("value", TypeSpecifier.FromType<object>(), MethodParameterPassType.Default, false, null)],
            [], MethodModifiers.Static, MemberVisibility.Public, TypeSpecifier.FromType<object>(), []);
        var call = new CallMethodNode(method, sink);
        GraphUtil.ConnectDataPins(getter.ValuePin, call.InputDataPins[0]);

        // A setter wired from a literal, on an exec chain.
        graph.GetSetChooser.Open(local.Specifier, position);
        graph.GetSetChooser.SetCommand.Execute(null);
        var setter = method.Nodes.OfType<VariableSetterNode>().Single();
        setter.PositionX = 42;
        setter.PositionY = 24;
        var literal = new LiteralNode(method, TypeSpecifier.FromType<object>());
        GraphUtil.ConnectDataPins(literal.ValuePin, setter.NewValuePin);
        GraphUtil.ConnectExecPins(method.EntryNode.InitialExecutionPin, setter.InputExecPins[0]);
        GraphUtil.ConnectExecPins(setter.OutputExecPins[0], method.MainReturnNode.ReturnPin);

        string getterId = getter.Id;
        string setterId = setter.Id;

        editor.Dialogs.TypeAnswer = TypeSpecifier.FromType<int>();
        await local.RetypeCommand.ExecuteAsync(null);

        var replacementGetter = method.Nodes.OfType<VariableGetterNode>().Single();
        var replacementSetter = method.Nodes.OfType<VariableSetterNode>().Single();
        Assert.NotSame(getter, replacementGetter);
        Assert.NotSame(setter, replacementSetter);
        string replacementGetterId = replacementGetter.Id;
        string replacementSetterId = replacementSetter.Id;

        for (int cycle = 0; cycle < 3; cycle++)
        {
            vm.UndoCommand.Execute(null);

            var restoredGetter = method.Nodes.OfType<VariableGetterNode>().Single();
            var restoredSetter = method.Nodes.OfType<VariableSetterNode>().Single();
            Assert.Same(getter, restoredGetter); // same instance every cycle, not a rebuilt one
            Assert.Same(setter, restoredSetter);
            Assert.Equal(getterId, restoredGetter.Id); // ids do not change across undo/redo cycles
            Assert.Equal(setterId, restoredSetter.Id);
            Assert.Equal(TypeSpecifier.FromType<object>(), restoredGetter.Variable.Type);
            Assert.Equal(10, restoredGetter.PositionX);
            Assert.Equal(20, restoredGetter.PositionY);
            Assert.Equal(42, restoredSetter.PositionX);
            Assert.Equal(24, restoredSetter.PositionY);
            Assert.Same(call.InputDataPins[0], restoredGetter.ValuePin.OutgoingPins.Single()); // getter's data wire restored
            Assert.Same(literal.ValuePin, restoredSetter.NewValuePin.IncomingPin); // setter's data wire restored
            Assert.Same(restoredSetter.InputExecPins[0], method.EntryNode.InitialExecutionPin.OutgoingPin);
            Assert.Same(method.MainReturnNode.ReturnPin, restoredSetter.OutputExecPins[0].OutgoingPin);

            vm.RedoCommand.Execute(null);

            var redoneGetter = method.Nodes.OfType<VariableGetterNode>().Single();
            var redoneSetter = method.Nodes.OfType<VariableSetterNode>().Single();
            Assert.Same(replacementGetter, redoneGetter); // the same replacement instance every redo
            Assert.Same(replacementSetter, redoneSetter);
            Assert.Equal(replacementGetterId, redoneGetter.Id); // ids stay the same, not minted fresh each redo
            Assert.Equal(replacementSetterId, redoneSetter.Id);
            Assert.Equal(TypeSpecifier.FromType<int>(), redoneGetter.Variable.Type);
            Assert.Equal(10, redoneGetter.PositionX);
            Assert.Equal(20, redoneGetter.PositionY);
            Assert.Same(redoneSetter.InputExecPins[0], method.EntryNode.InitialExecutionPin.OutgoingPin);
            Assert.Same(method.MainReturnNode.ReturnPin, redoneSetter.OutputExecPins[0].OutgoingPin);
        }
    }

    [Fact]
    public async Task RetypeLocalVariablePreservesChainedSetterConnectionsAndOrderUndoRedo()
    {
        // F-05: S1 -> S2, both setters of the same local. S1's snapshot must not end up pointing
        // at S2's replacement, and undo must restore both the connection and the node order.
        var local = CreateLocal();
        var position = new GraphPoint(100, 100);

        graph.GetSetChooser.Open(local.Specifier, position);
        graph.GetSetChooser.SetCommand.Execute(null);
        var s1 = method.Nodes.OfType<VariableSetterNode>().Single();
        s1.PositionX = 10;
        s1.PositionY = 20;

        graph.GetSetChooser.Open(local.Specifier, position);
        graph.GetSetChooser.SetCommand.Execute(null);
        var s2 = method.Nodes.OfType<VariableSetterNode>().Single(n => n != s1);
        s2.PositionX = 42;
        s2.PositionY = 24;

        GraphUtil.ConnectExecPins(method.EntryNode.InitialExecutionPin, s1.InputExecPins[0]);
        GraphUtil.ConnectExecPins(s1.OutputExecPins[0], s2.InputExecPins[0]); // S1 -> S2 chain
        GraphUtil.ConnectExecPins(s2.OutputExecPins[0], method.MainReturnNode.ReturnPin);

        var literal1 = new LiteralNode(method, TypeSpecifier.FromType<object>());
        GraphUtil.ConnectDataPins(literal1.ValuePin, s1.NewValuePin);
        var literal2 = new LiteralNode(method, TypeSpecifier.FromType<object>());
        GraphUtil.ConnectDataPins(literal2.ValuePin, s2.NewValuePin);

        var preRetypeNodeOrder = method.Nodes.ToList();

        editor.Dialogs.TypeAnswer = TypeSpecifier.FromType<int>();
        await local.RetypeCommand.ExecuteAsync(null);

        var replacements = method.Nodes.OfType<VariableSetterNode>().ToList();
        Assert.Equal(2, replacements.Count);
        var r1 = replacements.Single(n => n.PositionX == 10);
        var r2 = replacements.Single(n => n.PositionX == 42);
        Assert.Same(r2.InputExecPins[0], r1.OutputExecPins[0].OutgoingPin); // chain carried to the replacements
        Assert.Same(method.EntryNode.InitialExecutionPin.OutgoingPin, r1.InputExecPins[0]);
        Assert.Same(method.MainReturnNode.ReturnPin, r2.OutputExecPins[0].OutgoingPin);

        vm.UndoCommand.Execute(null);

        Assert.Equal(preRetypeNodeOrder, method.Nodes.ToList()); // same instances, same order
        Assert.Same(s2.InputExecPins[0], s1.OutputExecPins[0].OutgoingPin); // S1 -> S2 restored, not S1 -> R2
        Assert.Same(s1.InputExecPins[0], method.EntryNode.InitialExecutionPin.OutgoingPin);
        Assert.Same(method.MainReturnNode.ReturnPin, s2.OutputExecPins[0].OutgoingPin);
        Assert.Same(literal1.ValuePin, s1.NewValuePin.IncomingPin);
        Assert.Same(literal2.ValuePin, s2.NewValuePin.IncomingPin);
        Assert.DoesNotContain(r1, method.Nodes); // the replacement is fully detached, not left dangling
        Assert.DoesNotContain(r2, method.Nodes);

        vm.RedoCommand.Execute(null);

        Assert.Same(r1, method.Nodes.OfType<VariableSetterNode>().Single(n => n.PositionX == 10));
        Assert.Same(r2, method.Nodes.OfType<VariableSetterNode>().Single(n => n.PositionX == 42));
        Assert.Same(r2.InputExecPins[0], r1.OutputExecPins[0].OutgoingPin); // chain preserved again on redo
        Assert.Same(method.EntryNode.InitialExecutionPin.OutgoingPin, r1.InputExecPins[0]);
        Assert.Same(method.MainReturnNode.ReturnPin, r2.OutputExecPins[0].OutgoingPin);
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
        // reachable through the panel's drag & drop (NodeGraphViewModel.Drop(LocalVariableViewModel, GraphPoint)).
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
        using var search = new SuggestionListViewModel(graph);

        var rows = search.BuildItems(null);

        Assert.Contains(rows, r => r.IsHeader && r.Category == "Method Variables");
        Assert.Contains(rows, r => !r.IsHeader && r.Category == "Method Variables"
            && r.Value is VariableSpecifier v && v.Name == "Local" && v.Scope == VariableScope.Local);
    }
}
