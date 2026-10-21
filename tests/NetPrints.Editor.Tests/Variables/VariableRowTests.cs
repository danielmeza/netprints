using NetPrints.Core;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Tests.ProjectTree;
using NetPrints.Editor.Tests.Shell;
using NetPrints.Editor.Variables;
using DocumentId = NetPrints.Extensibility.Editor.DocumentId;

namespace NetPrints.Editor.Tests.Variables;

/// <summary>The rows of the Variables panel: selection shows the inspector, rename in place, remove and the type colour.</summary>
public sealed class VariableRowTests : IAsyncDisposable
{
    private readonly ShellPanelRig rig = new();

    public ValueTask DisposeAsync() => rig.DisposeAsync();

    private async Task<(ProjectSessionViewModel Session, ClassGraph Class, MethodGraph Method, VariablesPanelViewModel Panel)> OpenMethodAsync()
    {
        ProjectSessionViewModel session = await rig.OpenSessionAsync();
        ClassGraph cls = session.Project.Classes[0];
        var method = new MethodGraph("Greet") { Class = cls };
        cls.Methods.Add(method);
        rig.Shell.ActiveDocument = rig.Shell.AddDocument(new TestDocumentViewModel(DocumentId.Graph(session.ClassPathOf(cls), $"method:{method.Id}"), "doc"));
        return (session, cls, method, Assert.IsType<VariablesPanelViewModel>(rig.Variables.Current));
    }

    private static LocalVariableViewModel AddLocal(VariablesPanelViewModel panel)
    {
        panel.CreateLocalVariableCommand.Execute(null);
        return (panel.MethodVariables ?? throw new InvalidOperationException("No method graph is open."))[^1];
    }

    [Fact]
    public async Task SelectingALocalVariableShowsItInTheInspectorAndSelectingAMemberShowsTheVariableInspector()
    {
        (ProjectSessionViewModel session, ClassGraph cls, _, VariablesPanelViewModel panel) = await OpenMethodAsync();
        LocalVariableViewModel local = AddLocal(panel);
        session.ContextFor(cls).CreateVariable();
        MemberVariableViewModel member = panel.ClassVariables[^1];

        panel.SelectedLocalVariable = local;
        Assert.Same(local, rig.Inspector.Content);
        Assert.Null(panel.SelectedClassVariable);

        panel.SelectedClassVariable = member;
        Assert.Same(member, rig.Inspector.Content);
        Assert.Null(panel.SelectedLocalVariable);
    }

    [Fact]
    public async Task TheRowsFollowTheSelectionMadeElsewhere()
    {
        (ProjectSessionViewModel session, ClassGraph cls, _, VariablesPanelViewModel panel) = await OpenMethodAsync();
        session.ContextFor(cls).CreateVariable();

        rig.Shell.TreeSelection = cls.Variables[^1];
        Assert.Same(panel.ClassVariables[^1], panel.SelectedClassVariable);

        rig.Shell.TreeSelection = cls;
        Assert.Null(panel.SelectedClassVariable);
    }

    [Fact]
    public async Task RemovingTheSelectedLocalVariableEmptiesTheInspector()
    {
        (_, _, MethodGraph method, VariablesPanelViewModel panel) = await OpenMethodAsync();
        LocalVariableViewModel local = AddLocal(panel);
        panel.SelectedLocalVariable = local;

        local.RemoveCommand.Execute(null);

        Assert.Empty(method.LocalVariables);
        Assert.Null(panel.SelectedLocalVariable);
        Assert.True(rig.Inspector.IsEmpty);
    }

    [Fact]
    public async Task UndoingTheAddOfTheSelectedLocalVariableEmptiesTheInspector()
    {
        (ProjectSessionViewModel session, ClassGraph cls, MethodGraph method, VariablesPanelViewModel panel) = await OpenMethodAsync();
        LocalVariableViewModel local = AddLocal(panel);
        panel.SelectedLocalVariable = local;

        session.UndoStackFor(cls).Undo();

        Assert.Empty(method.LocalVariables);
        Assert.Null(panel.SelectedLocalVariable);
        Assert.True(rig.Inspector.IsEmpty);
    }

    [Fact]
    public async Task RemovingTheSelectedMemberVariableClearsTheSelection()
    {
        (ProjectSessionViewModel session, ClassGraph cls, _, VariablesPanelViewModel panel) = await OpenMethodAsync();
        session.ContextFor(cls).CreateVariable();
        MemberVariableViewModel member = panel.ClassVariables[^1];
        panel.SelectedClassVariable = member;

        member.RemoveCommand.Execute(null);

        Assert.Null(panel.SelectedClassVariable);
        Assert.True(rig.Inspector.IsEmpty);
    }

    [Fact]
    public async Task TheScopedDeleteAndRenameCommandsActOnTheRowPassedAsTheParameter()
    {
        (_, _, MethodGraph method, VariablesPanelViewModel panel) = await OpenMethodAsync();
        LocalVariableViewModel local = AddLocal(panel);

        Assert.False(rig.Invoker.TryRun("delete", CommandScope.VariablesPanel));
        Assert.True(rig.Invoker.TryRun("rename", CommandScope.VariablesPanel, local));
        await Task.Yield();
        Assert.True(local.IsEditing);
        Assert.False(rig.Invoker.TryRun("rename", CommandScope.VariablesPanel, local));
        Assert.False(rig.Invoker.TryRun("delete", CommandScope.VariablesPanel, local));

        local.CancelEditCommand.Execute(null);
        Assert.True(rig.Invoker.TryRun("delete", CommandScope.VariablesPanel, local));
        await Task.Yield();
        Assert.Empty(method.LocalVariables);
    }

    [Fact]
    public async Task RenamingALocalInPlaceIsOneUndoStep()
    {
        (ProjectSessionViewModel session, ClassGraph cls, MethodGraph method, VariablesPanelViewModel panel) = await OpenMethodAsync();
        LocalVariableViewModel local = AddLocal(panel);

        local.BeginRenameCommand.Execute(null);
        Assert.True(local.IsEditing);
        Assert.Equal("Local", local.EditText);
        local.EditText = "Count";
        local.CommitEditCommand.Execute(null);

        Assert.False(local.IsEditing);
        Assert.Equal("Count", method.LocalVariables.Single().Name);
        session.UndoStackFor(cls).Undo();
        Assert.Equal("Local", method.LocalVariables.Single().Name);
    }

    [Fact]
    public async Task RenamingAMemberInPlaceIsOneUndoStep()
    {
        (ProjectSessionViewModel session, ClassGraph cls, _, VariablesPanelViewModel panel) = await OpenMethodAsync();
        session.ContextFor(cls).CreateVariable();
        MemberVariableViewModel member = panel.ClassVariables[^1];
        string before = member.Name;

        member.BeginRenameCommand.Execute(null);
        member.EditText = "Counter";
        member.CommitEditCommand.Execute(null);

        Assert.False(member.IsEditing);
        Assert.Equal("Counter", cls.Variables[^1].Name);
        session.UndoStackFor(cls).Undo();
        Assert.Equal(before, cls.Variables[^1].Name);
    }

    [Fact]
    public async Task ARefusedNameKeepsTheRowEditingWithTheReasonAndEscapeLeavesIt()
    {
        (_, _, MethodGraph method, VariablesPanelViewModel panel) = await OpenMethodAsync();
        AddLocal(panel);
        LocalVariableViewModel second = AddLocal(panel);

        second.BeginRenameCommand.Execute(null);
        second.EditText = "Local";
        second.CommitEditCommand.Execute(null);

        Assert.True(second.IsEditing);
        Assert.True(second.HasRenameError);
        Assert.False(second.HasError);
        Assert.Equal(["Local", "Local2"], method.LocalVariables.Select(l => l.Name).ToArray());

        second.CancelEditCommand.Execute(null);
        Assert.False(second.IsEditing);
        Assert.False(second.HasRenameError);
        Assert.Equal("Local2", second.Name);
    }

    [Fact]
    public async Task ARefusalFromTheInspectorDoesNotShowInTheRowAndTheRowRenameDoesNotEraseIt()
    {
        (_, _, _, VariablesPanelViewModel panel) = await OpenMethodAsync();
        AddLocal(panel);
        LocalVariableViewModel second = AddLocal(panel);

        second.Name = "Local";
        Assert.True(second.HasError);
        Assert.False(second.HasRenameError);

        second.BeginRenameCommand.Execute(null);
        Assert.True(second.HasError);
        second.EditText = "Local";
        second.CommitEditCommand.Execute(null);
        Assert.True(second.HasRenameError);
        second.CancelEditCommand.Execute(null);

        Assert.False(second.HasRenameError);
        Assert.True(second.HasError);
    }

    [Fact]
    public async Task DeleteRemovesTheRowExceptWhileItsNameIsBeingEdited()
    {
        (ProjectSessionViewModel session, ClassGraph cls, _, VariablesPanelViewModel panel) = await OpenMethodAsync();
        session.ContextFor(cls).CreateVariable();
        MemberVariableViewModel member = panel.ClassVariables[^1];
        int count = cls.Variables.Count;

        member.BeginRenameCommand.Execute(null);
        Assert.False(member.RemoveCommand.CanExecute(null));
        member.CancelEditCommand.Execute(null);
        Assert.True(member.RemoveCommand.CanExecute(null));
        member.RemoveCommand.Execute(null);

        Assert.Equal(count - 1, cls.Variables.Count);
    }

    [Fact]
    public async Task TheRowChipUsesTheColourFamilyOfTheType()
    {
        (_, _, _, VariablesPanelViewModel panel) = await OpenMethodAsync();
        LocalVariableViewModel local = AddLocal(panel);
        Assert.Equal("Object", local.TypeName);

        local.Local.Type = TypeSpecifier.FromType<int>();

        Assert.Equal("pin-integer", local.FamilyClass);
        Assert.Equal("Int32", local.TypeName);
    }
}
