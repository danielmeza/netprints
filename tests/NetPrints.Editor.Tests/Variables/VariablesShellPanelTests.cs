using NetPrints.Core;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Tests.ProjectTree;
using NetPrints.Editor.Tests.Shell;
using NetPrints.Editor.Variables;
using DocumentId = NetPrints.Editor.Shell.DocumentId;

namespace NetPrints.Editor.Tests.Variables;

public sealed class VariablesShellPanelTests : IAsyncDisposable
{
    private readonly ShellPanelRig rig = new();

    public ValueTask DisposeAsync() => rig.DisposeAsync();

    private static void Activate(ShellViewModel shell, DocumentId id) =>
        shell.ActiveDocument = shell.AddDocument(new TestDocumentViewModel(id, "doc"));

    private async Task<(ProjectSessionViewModel Session, ClassGraph Class, MethodGraph Method)> OpenWithMethodAsync()
    {
        ProjectSessionViewModel session = await rig.OpenSessionAsync();
        ClassGraph cls = session.Project.Classes[0];
        var method = new MethodGraph("Greet") { Class = cls };
        cls.Methods.Add(method);
        return (session, cls, method);
    }

    private static DocumentId MethodDocument(ProjectSessionViewModel session, ClassGraph cls, MethodGraph method) =>
        DocumentId.Graph(session.ClassPathOf(cls), $"method:{method.Id}");

    [Fact]
    public async Task ThePanelIsEmptyWithNoActiveDocumentAndShowsTheActiveClassVariables()
    {
        (ProjectSessionViewModel session, ClassGraph cls, _) = await OpenWithMethodAsync();
        Assert.Null(rig.Variables.Current);

        Activate(rig.Shell, DocumentId.Graph(session.ClassPathOf(cls), DocumentId.ClassGraphKey));

        Assert.NotNull(rig.Variables.Current);
        Assert.Same(session.ContextFor(cls).Variables, rig.Variables.Current.ClassVariables);
        rig.Shell.ActiveDocument = null;
        Assert.Null(rig.Variables.Current);
    }

    [Fact]
    public async Task AddRemoveAndRenameAMemberVariable()
    {
        (ProjectSessionViewModel session, ClassGraph cls, _) = await OpenWithMethodAsync();
        Activate(rig.Shell, DocumentId.Graph(session.ClassPathOf(cls), DocumentId.ClassGraphKey));
        VariablesPanelViewModel panel = Assert.IsType<VariablesPanelViewModel>(rig.Variables.Current);
        int before = cls.Variables.Count;

        panel.CreateVariableCommand.Execute(null);

        Assert.Equal(before + 1, cls.Variables.Count);
        MemberVariableViewModel added = panel.ClassVariables[^1];
        added.Name = "Counter";
        Assert.Equal("Counter", cls.Variables[^1].Name);

        added.RemoveCommand.Execute(null);
        Assert.Equal(before, cls.Variables.Count);
        session.UndoStackFor(cls).Undo();
        Assert.Equal(before + 1, cls.Variables.Count);
    }

    [Fact]
    public async Task AddAndRemoveALocalVariableOfTheActiveMethod()
    {
        (ProjectSessionViewModel session, ClassGraph cls, MethodGraph method) = await OpenWithMethodAsync();
        Activate(rig.Shell, MethodDocument(session, cls, method));
        VariablesPanelViewModel panel = Assert.IsType<VariablesPanelViewModel>(rig.Variables.Current);
        Assert.True(panel.HasMethodGroup);
        Assert.Equal("Method: Greet", panel.MethodGroupHeader);

        panel.CreateLocalVariableCommand.Execute(null);

        LocalVariable local = Assert.Single(method.LocalVariables);
        Assert.Single(panel.MethodVariables ?? throw new InvalidOperationException());
        panel.MethodVariables[0].RemoveCommand.Execute(null);
        Assert.Empty(method.LocalVariables);
        Assert.NotNull(local);
    }

    [Fact]
    public async Task ThePanelFollowsTheActiveGraphAndClass()
    {
        (ProjectSessionViewModel session, ClassGraph cls, MethodGraph method) = await OpenWithMethodAsync();
        var other = new ClassGraph { Name = "Other", Namespace = cls.Namespace };
        session.Project.Classes.Add(other);

        Activate(rig.Shell, MethodDocument(session, cls, method));
        Assert.True(rig.Variables.Current?.HasMethodGroup);

        Activate(rig.Shell, DocumentId.Graph(session.ClassPathOf(cls), DocumentId.ClassGraphKey));
        Assert.False(rig.Variables.Current?.HasMethodGroup);

        Activate(rig.Shell, DocumentId.Graph(session.ClassPathOf(other), DocumentId.ClassGraphKey));
        Assert.Same(session.ContextFor(other).Variables, rig.Variables.Current?.ClassVariables);

        Activate(rig.Shell, MethodDocument(session, cls, method));
        Assert.Same(session.ContextFor(cls).Variables, rig.Variables.Current?.ClassVariables);
        Assert.True(rig.Variables.Current?.HasMethodGroup);
    }

    [Fact]
    public async Task SelectingAMemberVariableSelectsItInTheTree()
    {
        (ProjectSessionViewModel session, ClassGraph cls, _) = await OpenWithMethodAsync();
        Activate(rig.Shell, DocumentId.Graph(session.ClassPathOf(cls), DocumentId.ClassGraphKey));
        VariablesPanelViewModel panel = Assert.IsType<VariablesPanelViewModel>(rig.Variables.Current);
        panel.CreateVariableCommand.Execute(null);

        panel.ClassVariables[^1].SelectCommand.Execute(null);

        Assert.Same(cls.Variables[^1], rig.Shell.TreeSelection);
    }
}
