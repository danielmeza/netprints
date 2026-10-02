using NetPrints.Core;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.ProjectTree;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Tests.ProjectTree;
using NetPrints.Editor.UndoRedo;
using NetPrints.Editor.Variables;

namespace NetPrints.Editor.Tests.Inspectors;

public sealed class InspectorPanelViewModelTests : IAsyncDisposable
{
    private readonly ShellPanelRig rig = new();

    public ValueTask DisposeAsync() => rig.DisposeAsync();

    [Fact]
    public void WithNoProjectOpenTheInspectorIsEmpty()
    {
        Assert.Null(rig.Inspector.Content);
        Assert.True(rig.Inspector.IsEmpty);
        Assert.False(string.IsNullOrWhiteSpace(rig.Inspector.EmptyMessage));
    }

    [Fact]
    public async Task WithAProjectButNoSelectionTheInspectorIsEmptyAndClearingTheSelectionEmptiesItAgain()
    {
        ProjectSessionViewModel session = await rig.OpenSessionAsync();
        Assert.True(rig.Inspector.IsEmpty);

        rig.Tree.SelectedItem = rig.Item(TreeItemKind.Class, session.Project.Classes[0].Name);
        Assert.False(rig.Inspector.IsEmpty);
        rig.Tree.SelectedItem = null;
        Assert.True(rig.Inspector.IsEmpty);

        rig.Tree.SelectedItem = rig.Item(TreeItemKind.Class, session.Project.Classes[0].Name);
        rig.Shell.Session = null;
        Assert.True(rig.Inspector.IsEmpty);
    }

    [Fact]
    public async Task ASelectedClassShowsTheClassInspectorOverItsEditorViewModel()
    {
        ProjectSessionViewModel session = await rig.OpenSessionAsync();
        ClassGraph cls = session.Project.Classes[0];

        rig.Tree.SelectedItem = rig.Item(TreeItemKind.Class, cls.Name);

        ClassEditorViewModel editor = Assert.IsType<ClassEditorViewModel>(rig.Inspector.Content);
        Assert.Same(cls, editor.Class);
        Assert.Equal(cls.Name, editor.Name);
        rig.Tree.SelectedItem = rig.Tree.Roots[0];
        Assert.True(rig.Inspector.IsEmpty);
        rig.Tree.SelectedItem = rig.Item(TreeItemKind.Class, cls.Name);
        Assert.Same(editor, rig.Inspector.Content);
    }

    [Fact]
    public async Task AMethodOrConstructorShowsTheMethodInspector()
    {
        ProjectSessionViewModel session = await rig.OpenSessionAsync();
        ClassGraph cls = session.Project.Classes[0];
        var method = new MethodGraph("Greet") { Class = cls };
        var constructor = new ConstructorGraph { Class = cls };
        cls.Methods.Add(method);
        cls.Constructors.Add(constructor);

        rig.Tree.SelectedItem = rig.Item(TreeItemKind.Method, "Greet");
        MethodViewModel shownMethod = Assert.IsType<MethodViewModel>(rig.Inspector.Content);
        Assert.Same(method, shownMethod.Graph);
        Assert.False(shownMethod.IsConstructor);

        rig.Tree.SelectedItem = rig.Tree.Roots[0].Children[0].Children[1].Children[0];
        MethodViewModel shownConstructor = Assert.IsType<MethodViewModel>(rig.Inspector.Content);
        Assert.Same(constructor, shownConstructor.Graph);
        Assert.True(shownConstructor.IsConstructor);
    }

    [Fact]
    public async Task AVariableShowsTheVariableInspectorAndAnEventGraphOrGroupShowsNone()
    {
        ProjectSessionViewModel session = await rig.OpenSessionAsync();
        ClassGraph cls = session.Project.Classes[0];
        new UndoRedoStack().Do(EditorCommands.AddVariable(cls, "count"));
        cls.EventGraphs.Add(new EventGraph("Ticks") { Class = cls });

        rig.Tree.SelectedItem = rig.Item(TreeItemKind.Variable, "count");
        MemberVariableViewModel variable = Assert.IsType<MemberVariableViewModel>(rig.Inspector.Content);
        Assert.Same(cls.Variables[0], variable.Variable);

        rig.Tree.SelectedItem = rig.Item(TreeItemKind.EventGraph, "Ticks");
        Assert.True(rig.Inspector.IsEmpty);
        rig.Tree.SelectedItem = rig.Tree.Roots[0].Children[0].Children[0];
        Assert.True(rig.Inspector.IsEmpty);
    }

    [Fact]
    public async Task ASelectInspectorMessageFromAVariableSelectsItsRowAndTheInspectorFollows()
    {
        ProjectSessionViewModel session = await rig.OpenSessionAsync();
        ClassGraph cls = session.Project.Classes[0];
        new UndoRedoStack().Do(EditorCommands.AddVariable(cls, "count"));
        new UndoRedoStack().Do(EditorCommands.AddVariable(cls, "other"));
        rig.Tree.SelectedItem = rig.Item(TreeItemKind.Class, cls.Name);
        ClassEditorViewModel editor = Assert.IsType<ClassEditorViewModel>(rig.Inspector.Content);
        MemberVariableViewModel other = editor.Variables.Single(variable => variable.Variable.Name == "other");

        other.SelectCommand.Execute(null);

        Assert.Equal("other", rig.Tree.SelectedItem?.Name);
        Assert.Same(other, rig.Inspector.Content);
    }

    [Fact]
    public async Task ARemovedSelectionEmptiesTheInspector()
    {
        ProjectSessionViewModel session = await rig.OpenSessionAsync();
        ClassGraph cls = session.Project.Classes[0];
        var method = new MethodGraph("Greet") { Class = cls };
        cls.Methods.Add(method);
        rig.Tree.SelectedItem = rig.Item(TreeItemKind.Method, "Greet");
        Assert.False(rig.Inspector.IsEmpty);

        cls.Methods.Remove(method);

        Assert.True(rig.Inspector.IsEmpty);
        Assert.Null(rig.Shell.TreeSelection);
    }

    [Fact]
    public async Task RenamingTheClassInTheInspectorRenamesItsTreeRow()
    {
        ProjectSessionViewModel session = await rig.OpenSessionAsync();
        ClassGraph cls = session.Project.Classes[0];
        rig.Tree.SelectedItem = rig.Item(TreeItemKind.Class, cls.Name);

        Assert.IsType<ClassEditorViewModel>(rig.Inspector.Content).Name = "Renamed";

        Assert.Equal("Tree.class.Renamed", rig.Tree.SelectedItem.AutomationId);
    }
}
