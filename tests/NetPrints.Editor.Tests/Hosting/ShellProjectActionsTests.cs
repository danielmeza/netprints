using NetPrints.Core;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Tests.Shell;
using NetPrints.Editor.Variables;

namespace NetPrints.Editor.Tests.Hosting;

/// <summary>The project flows the shell window gives its commands, other than loading (see <see cref="ProjectLoaderTests"/>).</summary>
public sealed class ShellProjectActionsTests : IDisposable
{
    private readonly TestEditor testEditor = TestEditor.Create(TestEditor.CreateReflectionHost);
    private readonly List<string> cleanup = [];
    private readonly List<IDisposable> disposables = [];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        disposables.ForEach(item => item.Dispose());
        cleanup.ForEach(TestPaths.TryDelete);
    }

    private ProjectRig NewRig()
    {
        var rig = new ProjectRig(testEditor.Context);
        disposables.Add(rig);
        return rig;
    }

    private async Task<ProjectRig> OpenSampleAsync()
    {
        string path = TestPaths.CopyHelloWorldSample();
        cleanup.Add(path);
        ProjectRig rig = NewRig();
        await rig.LoadProjectAsync(path);
        return rig;
    }

    private ProjectRig OpenEmpty()
    {
        string dir = TestPaths.CreateTempDirectory();
        cleanup.Add(dir);
        var project = Project.FromSnapshot(TestSnapshots.Empty("P", "N"));
        project.Path = Path.Combine(dir, "P.csproj");
        ProjectRig rig = NewRig();
        var session = new ProjectSessionViewModel(project, testEditor.Context);
        disposables.Add(session);
        rig.Shell.Session = session;
        return rig;
    }

    [Fact]
    public async Task ExitClosesTheMainWindow()
    {
        ProjectRig rig = await OpenSampleAsync();

        await rig.Actions.ExitAsync(Token);

        Assert.Equal(1, testEditor.Windows.CloseMainWindowCount);
    }

    [Fact]
    public async Task UnloadIsAlwaysConfirmedUntilTheDirtyPromptExists()
    {
        ProjectRig rig = await OpenSampleAsync();

        Assert.True(await rig.Actions.ConfirmUnloadAsync(Token));
    }

    [Fact]
    public void ProjectSettingsOpensTheSettingsDocument()
    {
        ProjectRig rig = NewRig();
        var shell = new FakeShell();
        rig.Actions.Api = shell;

        rig.Actions.ShowProjectSettings();

        Assert.Equal([$"OpenDocument:{DocumentId.ProjectSettings}"], shell.Calls);
    }

    [Fact]
    public async Task ReferencesShowsTheDialogForTheOpenProject()
    {
        ProjectRig rig = await OpenSampleAsync();

        await rig.Actions.ShowReferencesAsync(Token);

        Assert.Same(rig.Project, testEditor.Dialogs.ReferenceDialogs.Single().Project);
    }

    [Fact]
    public async Task ReferencesWithNoProjectOpenShowsNothing()
    {
        ProjectRig rig = NewRig();

        await rig.Actions.ShowReferencesAsync(Token);

        Assert.Empty(testEditor.Dialogs.ReferenceDialogs);
    }

    [Fact]
    public async Task RenamingANeverSavedClassKeepsItsOpenTabsResolving()
    {
        ProjectRig rig = OpenEmpty();
        var api = new FakeShell();
        rig.Actions.Api = api;
        await rig.Actions.NewClassAsync(Token);
        await rig.Actions.NewClassAsync(Token);
        ProjectSessionViewModel session = Assert.IsType<ProjectSessionViewModel>(rig.Session);
        ClassGraph renamed = session.Project.Classes[0];
        ClassGraph other = session.Project.Classes[1];
        DocumentId id = DocumentId.Graph(session.ClassPathOf(renamed), DocumentId.ClassGraphKey);
        api.OpenDocument(id);

        renamed.Name = "Renamed";
        rig.Actions.AddVariable(other);

        Assert.Contains(id, api.OpenDocuments);
        Assert.Same(renamed, CommandTargets.GraphOf(session, id));
        Assert.Equal(id, CommandTargets.GraphDocumentOf(session, renamed));
    }

    [Fact]
    public async Task NewClassNamesAreUnique()
    {
        ProjectRig rig = OpenEmpty();
        Project project = Assert.IsType<Project>(rig.Project);

        await rig.Actions.NewClassAsync(Token);
        await rig.Actions.NewClassAsync(Token);

        Assert.Equal(new[] { "MyClass", "MyClass2" }, project.Classes.Select(c => c.Name).ToArray());
        Assert.True(project.Classes.All(c => c.Namespace == "N"));
    }

    [Fact]
    public async Task ExistingClassIsCopiedIntoProjectAndErrorsAreShown()
    {
        TestEditor editor = testEditor;
        string dir = TestPaths.CreateTempDirectory();
        cleanup.Add(dir);
        string csprojPath = Path.Combine(dir, "P.csproj");
        File.WriteAllText(csprojPath, "<Project />");
        ProjectRig rig = NewRig();
        await rig.LoadProjectAsync(csprojPath);
        var project = rig.Project;
        Assert.NotNull(project);

        // A real .netpc.json file elsewhere, built and saved through a second, throwaway project.
        string sourceDir = TestPaths.CreateTempDirectory();
        cleanup.Add(sourceDir);
        string sourceCsprojPath = Path.Combine(sourceDir, "Source.csproj");
        File.WriteAllText(sourceCsprojPath, "<Project />");
        ProjectRig source = NewRig();
        await source.LoadProjectAsync(sourceCsprojPath);
        var sourceProject = source.Project;
        Assert.NotNull(sourceProject);
        await source.NewClassAsync();
        var sourceClass = sourceProject.Classes.Single();
        await editor.Context.Persistence.SaveAsync(sourceProject, _ => "// generated\n", Token);
        string sourceGraphPath = sourceProject.GetGraphFilePath(sourceClass);

        editor.FilePicker.OpenFileAnswers.Enqueue(sourceGraphPath);
        await rig.Actions.AddExistingClassAsync(Token);
        Assert.Equal(sourceClass.FullName, project.Classes.Single().FullName);
        Assert.True(File.Exists(Path.Combine(dir, Path.GetFileName(sourceGraphPath))));
        Assert.Contains("*.netpc.json", editor.FilePicker.Calls.Single());

        string bad = Path.Combine(dir, "Bad.netpc.json");
        File.WriteAllText(bad, "garbage");
        editor.FilePicker.OpenFileAnswers.Enqueue(bad);
        await rig.Actions.AddExistingClassAsync(Token);
        Assert.Equal("Failed to load existing class", editor.Dialogs.Errors.Single().Title);
    }

    [Fact]
    public async Task AddingAnExistingClassAsksNothingWithNoProjectOpen()
    {
        ProjectRig rig = NewRig();

        await rig.Actions.AddExistingClassAsync(Token);

        Assert.Empty(testEditor.FilePicker.Calls);
    }

    [Fact]
    public async Task DeleteItemRemovesAClassAndClosesItsDocuments()
    {
        ProjectRig rig = await OpenSampleAsync();
        var shell = new FakeShell();
        rig.Actions.Api = shell;
        ClassGraph cls = Assert.Single(rig.Project?.Classes ?? []);
        DocumentId id = CommandTargets.GraphDocumentOf(rig.Session ?? throw new InvalidOperationException("No session."), cls)
            ?? throw new InvalidOperationException("No document id.");
        shell.OpenDocument(id);

        await rig.Actions.DeleteItemAsync(cls, Token);

        Assert.Empty(rig.Project?.Classes ?? [cls]);
        Assert.Empty(shell.OpenDocuments);
        Assert.Equal(cls.Name, Assert.Single(testEditor.Dialogs.ConfirmCalls).Message.Split('\'')[1]);
    }

    [Fact]
    public async Task DeleteItemKeepsAClassTheUserDidNotConfirmRemoving()
    {
        ProjectRig rig = await OpenSampleAsync();
        var shell = new FakeShell();
        rig.Actions.Api = shell;
        ClassGraph cls = Assert.Single(rig.Project?.Classes ?? []);
        DocumentId id = CommandTargets.GraphDocumentOf(rig.Session ?? throw new InvalidOperationException("No session."), cls)
            ?? throw new InvalidOperationException("No document id.");
        shell.OpenDocument(id);
        testEditor.Dialogs.ConfirmAnswer = false;

        await rig.Actions.DeleteItemAsync(cls, Token);

        Assert.Same(cls, Assert.Single(rig.Project?.Classes ?? []));
        Assert.Single(shell.OpenDocuments);
    }

    [Theory]
    [InlineData("getter")]
    [InlineData("setter")]
    [InlineData("type")]
    public async Task AnOpenGraphMessageOpensTheVariableGraphAsADocument(string which)
    {
        ProjectRig rig = await OpenSampleAsync();
        var shell = new FakeShell();
        rig.Actions.Api = shell;
        ProjectSessionViewModel session = rig.Session ?? throw new InvalidOperationException("No session.");
        ClassContext classContext = session.ContextFor(Assert.Single(session.Project.Classes));
        classContext.CreateVariable();
        MemberVariableViewModel variable = classContext.Variables[^1];
        variable.AddGetterCommand.Execute(null);
        variable.AddSetterCommand.Execute(null);

        (which switch
        {
            "getter" => variable.OpenGetterCommand,
            "setter" => variable.OpenSetterCommand,
            _ => variable.OpenTypeGraphCommand,
        }).Execute(null);

        DocumentId id = shell.ActiveDocument ?? throw new InvalidOperationException("No document was opened.");
        NodeGraph expected = which switch
        {
            "getter" => variable.Getter ?? throw new InvalidOperationException("No getter."),
            "setter" => variable.Setter ?? throw new InvalidOperationException("No setter."),
            _ => variable.Variable.TypeGraph,
        };
        Assert.StartsWith(which + ":", id.GraphKey, StringComparison.Ordinal);
        Assert.Same(expected, CommandTargets.GraphOf(session, id));
        Assert.Same(session.Project.Classes[0], CommandTargets.ClassOf(expected));
        Assert.Equal(id, CommandTargets.GraphDocumentOf(session, expected));
    }

    [Fact]
    public async Task UndoingTheCreationOfAnEventGraphClosesItsTabAndRedoDoesNotReopenIt()
    {
        ProjectRig rig = await OpenSampleAsync();
        var shell = new FakeShell();
        rig.Actions.Api = shell;
        ProjectSessionViewModel session = rig.Session ?? throw new InvalidOperationException("No session.");
        ClassGraph cls = Assert.Single(session.Project.Classes);

        rig.Actions.AddEventGraph(cls);
        Assert.Single(shell.OpenDocuments);

        session.UndoStackFor(cls).Undo();
        Assert.Empty(shell.OpenDocuments);

        session.UndoStackFor(cls).Redo();
        Assert.Empty(shell.OpenDocuments);
    }

    [Theory]
    [InlineData("method")]
    [InlineData("ctor")]
    public async Task UndoingTheCreationOfAMethodOrConstructorClosesItsTabAndRedoDoesNotReopenIt(string which)
    {
        ProjectRig rig = await OpenSampleAsync();
        var shell = new FakeShell();
        rig.Actions.Api = shell;
        ProjectSessionViewModel session = rig.Session ?? throw new InvalidOperationException("No session.");
        ClassGraph cls = Assert.Single(session.Project.Classes);

        if (which == "method")
        {
            rig.Actions.AddMethod(cls);
        }
        else
        {
            rig.Actions.AddConstructor(cls);
        }

        Assert.Single(shell.OpenDocuments);

        session.UndoStackFor(cls).Undo();
        Assert.Empty(shell.OpenDocuments);
        Assert.Empty(which == "method" ? cls.Methods.Where(m => m.Name == "Method") : cls.Constructors);

        session.UndoStackFor(cls).Redo();
        Assert.Empty(shell.OpenDocuments);
    }

    [Fact]
    public async Task OverridingAMethodAsksForOneOpensItAndUndoClosesItsTab()
    {
        ProjectRig rig = await OpenSampleAsync();
        var shell = new FakeShell();
        rig.Actions.Api = shell;
        ProjectSessionViewModel session = rig.Session ?? throw new InvalidOperationException("No session.");
        ClassGraph cls = Assert.Single(session.Project.Classes);
        await testEditor.Context.Reflection.Loaded;
        testEditor.Dialogs.MethodAnswer = methods => methods.First(m => m.Name == "ToString");

        await rig.Actions.OverrideMethodAsync(cls, Token);

        Assert.Equal(1, testEditor.Dialogs.SelectMethodCalls);
        MethodGraph method = Assert.Single(cls.Methods, m => m.Name == "ToString");
        Assert.Equal(CommandTargets.GraphDocumentOf(session, method), shell.ActiveDocument);

        session.UndoStackFor(cls).Undo();
        Assert.DoesNotContain(method, cls.Methods);
        Assert.Empty(shell.OpenDocuments);
    }

    [Fact]
    public async Task CancellingTheOverrideChooserChangesNothing()
    {
        ProjectRig rig = await OpenSampleAsync();
        var shell = new FakeShell();
        rig.Actions.Api = shell;
        ClassGraph cls = Assert.Single((rig.Session ?? throw new InvalidOperationException("No session.")).Project.Classes);
        await testEditor.Context.Reflection.Loaded;
        testEditor.Dialogs.MethodAnswer = _ => null;
        int methods = cls.Methods.Count;

        await rig.Actions.OverrideMethodAsync(cls, Token);

        Assert.Equal(methods, cls.Methods.Count);
        Assert.Empty(shell.OpenDocuments);
    }

    [Fact]
    public async Task UndoingTheCreationOfAnAccessorClosesItsTabButKeepsTheOthers()
    {
        ProjectRig rig = await OpenSampleAsync();
        var shell = new FakeShell();
        rig.Actions.Api = shell;
        ProjectSessionViewModel session = rig.Session ?? throw new InvalidOperationException("No session.");
        ClassGraph cls = Assert.Single(session.Project.Classes);
        ClassContext classContext = session.ContextFor(cls);
        classContext.CreateVariable();
        MemberVariableViewModel variable = classContext.Variables[^1];
        rig.Actions.AddEventGraph(cls);
        variable.AddGetterCommand.Execute(null);
        variable.OpenGetterCommand.Execute(null);
        Assert.Equal(2, shell.OpenDocuments.Count);

        session.UndoStackFor(cls).Undo();

        DocumentId remaining = Assert.Single(shell.OpenDocuments);
        Assert.StartsWith(DocumentId.EventKeyPrefix, remaining.GraphKey, StringComparison.Ordinal);
    }
}
