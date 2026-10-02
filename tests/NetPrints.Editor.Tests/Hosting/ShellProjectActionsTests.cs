using NetPrints.Core;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Tests.Shell;

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

        rig.Actions.DeleteItem(cls);

        Assert.Empty(rig.Project?.Classes ?? [cls]);
        Assert.Empty(shell.OpenDocuments);
    }
}
