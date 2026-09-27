using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Core;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Main;
using NetPrints.Editor.References;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Graph;
using NetPrints.Projects;

namespace NetPrints.Editor.Tests.Main;

public class MainEditorVMTests : IDisposable
{
    // MainEditorVM reloads the reflection host fire-and-forget whenever a project is set; a private
    // host keeps those reloads from replacing the provider the shared host serves to other tests.
    private readonly TestEditor testEditor = new(new ReflectionHost(new InlineDispatcher(), NullLogger<ReflectionHost>.Instance));
    private readonly List<string> cleanup = [];

    public void Dispose() => cleanup.ForEach(TestPaths.TryDelete);

    private string Track(string path)
    {
        cleanup.Add(path);
        return path;
    }

    [Fact]
    public void PanesAreMutuallyExclusiveAndSaveNeedsProject()
    {
        var editor = testEditor;
        var vm = new MainEditorVM(editor.Context);

        Assert.False(vm.IsProjectOpen);
        Assert.False(vm.SaveProjectCommand.CanExecute(null));
        Assert.False(vm.ToggleSettingsPaneCommand.CanExecute(null), "Settings are disabled without a project (PAR-07)");
        Assert.False(vm.ShowReferencesCommand.CanExecute(null), "References are disabled without a project (PAR-08)");

        vm.ToggleProjectPaneCommand.Execute(null);
        Assert.True(vm.IsProjectPaneOpen);

        vm.Project = Project.FromSnapshot(TestSnapshots.Empty("P", "N"));
        Assert.True(vm.SaveProjectCommand.CanExecute(null));
        vm.ToggleSettingsPaneCommand.Execute(null);
        Assert.True(vm.IsSettingsPaneOpen);
        Assert.False(vm.IsProjectPaneOpen, "Project and Settings panes are exclusive (PAR-02)");

        vm.ToggleProjectPaneCommand.Execute(null);
        Assert.True(vm.IsProjectPaneOpen);
        Assert.False(vm.IsSettingsPaneOpen);
    }

    [Fact]
    public async Task CreateProjectCancelKeepsPreviousProject()
    {
        var editor = testEditor;
        var previous = Project.FromSnapshot(TestSnapshots.Empty("Previous", "Prev"));
        var vm = new MainEditorVM(editor.Context, previous);

        editor.FilePicker.SaveFileAnswers.Enqueue(null);
        await vm.CreateProjectCommand.ExecuteAsync(null);

        Assert.Same(previous, vm.Project);
        Assert.Contains("MyProject.csproj", editor.FilePicker.Calls.Single());
        Assert.Contains("*.csproj", editor.FilePicker.Calls.Single());
    }

    [Fact]
    public async Task CreateProjectTakesNameFromFileAndOpensIt()
    {
        var editor = testEditor;
        var vm = new MainEditorVM(editor.Context);
        string dir = Track(TestPaths.CreateTempDirectory());
        string path = Path.Combine(dir, "Chosen.csproj");

        editor.FilePicker.SaveFileAnswers.Enqueue(path);
        await vm.CreateProjectCommand.ExecuteAsync(null);

        var project = vm.Project;
        Assert.NotNull(project);
        Assert.Equal("Chosen", project.Name);
        Assert.Equal("Chosen", project.DefaultNamespace);
        Assert.True(File.Exists(path));
        Assert.Equal("Chosen", vm.Title);
    }

    [Fact]
    public async Task OpeningANetppShowsAMessageAndWritesNothing()
    {
        var editor = testEditor;
        var vm = new MainEditorVM(editor.Context);
        string dir = Track(TestPaths.CreateTempDirectory());
        string legacy = Path.Combine(dir, "Legacy.netpp");
        File.WriteAllText(legacy, "not a real project");

        editor.FilePicker.OpenFileAnswers.Enqueue(legacy);
        await vm.OpenProjectCommand.ExecuteAsync(null);

        Assert.Null(vm.Project);
        Assert.False(vm.IsBusy);
        Assert.Equal("Unsupported project file", editor.Dialogs.Errors.Single().Title);
        Assert.Empty(editor.Projects.LoadCalls);
    }

    [Fact]
    public async Task OpenFailureShowsErrorAndCopiesException()
    {
        var editor = testEditor;
        var vm = new MainEditorVM(editor.Context);
        string dir = Track(TestPaths.CreateTempDirectory());
        string missing = Path.Combine(dir, "Missing.csproj");

        editor.FilePicker.OpenFileAnswers.Enqueue(missing);
        await vm.OpenProjectCommand.ExecuteAsync(null);

        Assert.Null(vm.Project);
        Assert.False(vm.IsBusy);
        Assert.Equal(1, editor.Dialogs.Errors.Count());
        Assert.Equal("Failed to load project", editor.Dialogs.Errors[0].Title);
        Assert.NotNull(editor.Clipboard.Text);
        Assert.Contains(editor.Clipboard.Text!, editor.Dialogs.Errors[0].Message);
    }

    [Fact]
    public async Task StartupArgumentOpensProject()
    {
        var editor = testEditor;
        var vm = new MainEditorVM(editor.Context);
        string path = Track(TestPaths.CopyHelloWorldSample());

        await vm.OpenStartupProjectAsync([path]);

        var project = vm.Project;
        Assert.NotNull(project);
        Assert.Equal("HelloWorld", project.Name);
        Assert.Equal("HelloWorld.Program", project.Classes.Single().FullName);

        // More than one argument is ignored.
        var other = new MainEditorVM(editor.Context);
        await other.OpenStartupProjectAsync([path, path]);
        Assert.Null(other.Project);
    }

    [Fact]
    public async Task SaveWritesOnlyDirtyClasses()
    {
        var editor = testEditor;
        string path = Track(TestPaths.CopyHelloWorldSample());
        var vm = new MainEditorVM(editor.Context);
        await vm.LoadProjectAsync(path);

        var project = vm.Project;
        Assert.NotNull(project);
        var cls = project.Classes.Single();
        Assert.False(cls.IsDirty);

        // Nothing dirty: still reports success, nothing to check on disk.
        Assert.True(await vm.PromptProjectSaveAsync());

        cls.MarkDirty();
        string graphPath = project.GetGraphFilePath(cls);
        string generatedPath = Path.Combine(Path.GetDirectoryName(graphPath) ?? "", Path.GetFileNameWithoutExtension(graphPath) + ".g.cs");
        File.Delete(graphPath);
        File.Delete(generatedPath);

        Assert.True(await vm.PromptProjectSaveAsync());

        Assert.True(File.Exists(graphPath));
        Assert.True(File.Exists(generatedPath));
        Assert.False(cls.IsDirty);
    }

    [Fact]
    public async Task ChangingOutputBinaryTypeAppliesThroughProjectSystem()
    {
        var editor = testEditor;
        string path = Track(TestPaths.CopyHelloWorldSample());
        var vm = new MainEditorVM(editor.Context);
        await vm.LoadProjectAsync(path);

        var project = vm.Project;
        Assert.NotNull(project);
        Assert.Equal(BinaryType.Executable, vm.OutputBinaryType);

        vm.OutputBinaryType = BinaryType.SharedLibrary;

        Assert.Equal(BinaryType.SharedLibrary, vm.OutputBinaryType);
        Assert.Equal(BinaryType.SharedLibrary, project.OutputBinaryType);
        Assert.Equal(BinaryType.SharedLibrary, project.Snapshot?.OutputType);
    }

    [Fact]
    public async Task RunCompilesThenStartsProgramThroughLauncher()
    {
        var editor = testEditor;
        string path = Track(TestPaths.CopyHelloWorldSample());
        var vm = new MainEditorVM(editor.Context);
        await vm.LoadProjectAsync(path);

        Assert.True(vm.CanCompileAndRun);
        await vm.RunCommand.ExecuteAsync(null);

        Assert.True(vm.Project?.LastCompilationSucceeded);
        Assert.Equal("Build succeeded", vm.Project?.CompilationMessage);
        Assert.Equal(1, editor.Processes.Started.Count());
    }

    [Fact]
    public async Task CompileReportsErrors()
    {
        var editor = testEditor;
        string dir = Track(TestPaths.CreateTempDirectory());
        var project = Project.FromSnapshot(TestSnapshots.Empty("Broken", "N"));
        project.Path = Path.Combine(dir, "Broken.csproj");
        editor.Projects.BuildResultFactory = _ => new BuildResult(false,
            [new ProjectMessage(ProjectMessageSeverity.Error, "CS0006", "Metadata file 'missing.dll' could not be found", null, null, null)],
            null, "");
        var vm = new MainEditorVM(editor.Context, project);

        await vm.CompileCommand.ExecuteAsync(null);

        Assert.False(project.LastCompilationSucceeded);
        Assert.Equal("Build failed with 1 error(s)", project.CompilationMessage);
        Assert.Contains(project.LastDiagnostics, d => d.Message.Contains("missing.dll"));
        Assert.False(vm.RunCommand.CanExecute(null), "library projects cannot run");
    }

    [Fact]
    public async Task CompileReportsATranslationFailureAsABuildError()
    {
        var editor = testEditor;
        string path = Track(TestPaths.CopyHelloWorldSample());
        var vm = new MainEditorVM(editor.Context);
        await vm.LoadProjectAsync(path);
        var project = vm.Project;
        Assert.NotNull(project);
        var cls = project.Classes.Single();
        cls.Methods.Single().Nodes.OfType<CallMethodNode>().Single().InputDataPins.Single().UnconnectedValue = null;
        cls.MarkDirty();

        await vm.CompileCommand.ExecuteAsync(null);

        Assert.False(project.LastCompilationSucceeded);
        Assert.Equal("Build failed with 1 error(s)", project.CompilationMessage);
        Assert.Equal(cls.FullName, project.LastDiagnostics.Single().ClassFullName);
        Assert.Empty(editor.Dialogs.Errors);
    }

    [Fact]
    public async Task NewClassNamesAreUnique()
    {
        var editor = testEditor;
        string dir = Track(TestPaths.CreateTempDirectory());
        var project = Project.FromSnapshot(TestSnapshots.Empty("P", "N"));
        project.Path = Path.Combine(dir, "P.csproj");
        var vm = new MainEditorVM(editor.Context, project);

        await vm.NewClassCommand.ExecuteAsync(null);
        await vm.NewClassCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "MyClass", "MyClass2" }, project.Classes.Select(c => c.Name).ToArray());
        Assert.True(project.Classes.All(c => c.Namespace == "N"));
    }

    [Fact]
    public async Task ExistingClassIsCopiedIntoProjectAndErrorsAreShown()
    {
        var editor = testEditor;
        string dir = Track(TestPaths.CreateTempDirectory());
        string csprojPath = Path.Combine(dir, "P.csproj");
        File.WriteAllText(csprojPath, "<Project />");
        var vm = new MainEditorVM(editor.Context);
        await vm.LoadProjectAsync(csprojPath);
        var project = vm.Project;
        Assert.NotNull(project);

        // A real .netpc.json file elsewhere, built and saved through a second, throwaway project.
        string sourceDir = Track(TestPaths.CreateTempDirectory());
        string sourceCsprojPath = Path.Combine(sourceDir, "Source.csproj");
        File.WriteAllText(sourceCsprojPath, "<Project />");
        var sourceVm = new MainEditorVM(editor.Context);
        await sourceVm.LoadProjectAsync(sourceCsprojPath);
        var sourceProject = sourceVm.Project;
        Assert.NotNull(sourceProject);
        await sourceVm.NewClassCommand.ExecuteAsync(null);
        var sourceClass = sourceProject.Classes.Single();
        await editor.Context.Persistence.SaveAsync(sourceProject, _ => "// generated\n", TestContext.Current.CancellationToken);
        string sourceGraphPath = sourceProject.GetGraphFilePath(sourceClass);

        editor.FilePicker.OpenFileAnswers.Enqueue(sourceGraphPath);
        await vm.AddExistingClassCommand.ExecuteAsync(null);
        Assert.Equal(sourceClass.FullName, project.Classes.Single().FullName);
        Assert.True(File.Exists(Path.Combine(dir, Path.GetFileName(sourceGraphPath))));
        Assert.Contains("*.netpc.json", editor.FilePicker.Calls.Single());

        string bad = Path.Combine(dir, "Bad.netpc.json");
        File.WriteAllText(bad, "garbage");
        editor.FilePicker.OpenFileAnswers.Enqueue(bad);
        await vm.AddExistingClassCommand.ExecuteAsync(null);
        Assert.Equal("Failed to load existing class", editor.Dialogs.Errors.Single().Title);
    }

    [Fact]
    public async Task OpenClassReusesWindowAndRemoveClassClosesIt()
    {
        var editor = testEditor;
        var project = await TestPaths.LoadHelloWorldCopyAsync(TestContext.Current.CancellationToken);
        Track(project.Path);
        var vm = new MainEditorVM(editor.Context, project);
        var cls = project.Classes.Single();

        vm.OpenClassCommand.Execute(cls);
        Assert.Equal(1, editor.Windows.Open.Count());
        var firstEditor = editor.Windows.Open[cls];

        vm.OpenClassCommand.Execute(cls);
        Assert.Same(firstEditor, editor.Windows.Open[cls]);
        Assert.Equal(new[] { cls }, editor.Windows.Activated);

        vm.RemoveClassCommand.Execute(cls);
        Assert.Empty(editor.Windows.Open);
        Assert.Equal(new[] { cls }, editor.Windows.Closed);
        Assert.Empty(project.Classes);
    }

    [Fact]
    public async Task ClosingMainWindowClosesAllClassWindows()
    {
        var editor = testEditor;
        var project = await TestPaths.LoadHelloWorldCopyAsync(TestContext.Current.CancellationToken);
        Track(project.Path);
        var vm = new MainEditorVM(editor.Context, project);
        vm.OpenClassCommand.Execute(project.Classes.Single());

        vm.OnMainWindowClosed();

        Assert.Empty(editor.Windows.Open);
        Assert.Equal(1, editor.Windows.CloseAllCount);
    }

    [Fact]
    public async Task ReferencesDialogOpensForProject()
    {
        var editor = testEditor;
        var project = Project.FromSnapshot(TestSnapshots.Empty("P", "N"));
        var vm = new MainEditorVM(editor.Context, project);

        await vm.ShowReferencesCommand.ExecuteAsync(null);

        Assert.Same(project, editor.Dialogs.ReferenceDialogs.Single().Project);
    }

    [Fact(Timeout = 120000)]
    public async Task ReflectionReloadsOnOpenAndOnReferencesChange()
    {
        var editor = new TestEditor(new ReflectionHost(new InlineDispatcher(), NullLogger<ReflectionHost>.Instance));
        int reloads = 0;
        editor.Reflection.Reloaded += (_, _) => reloads++;

        string path = Track(TestPaths.CopyHelloWorldSample());
        var vm = new MainEditorVM(editor.Context);
        await vm.LoadProjectAsync(path);
        await WaitFor(() => reloads >= 1);
        Assert.True(editor.Reflection.NonStaticTypes.Count > 4000, "type list refreshed");

        var project = vm.Project;
        Assert.NotNull(project);
        using var references = new ReferenceListVM(project, editor.Context);
        await references.AddSourceDirectoryAsync(Path.GetDirectoryName(path) ?? "");
        await WaitFor(() => reloads >= 2);
    }

    [Fact]
    public async Task NoSdkShowsAnErrorDialogInsteadOfCrashing()
    {
        var noSdkProjects = new NoSdkProjectSystem();
        var noSdkContext = testEditor.Context with { Projects = noSdkProjects, Persistence = TestEditor.CreatePersistence(noSdkProjects) };
        var vm = new MainEditorVM(noSdkContext);
        string path = Track(TestPaths.CopyHelloWorldSample());

        await vm.LoadProjectAsync(path);

        Assert.Null(vm.Project);
        var (title, message) = testEditor.Dialogs.Errors.Single();
        Assert.Equal("Failed to load project", title);
        Assert.Contains(nameof(ProjectSystemException), message);
        Assert.Contains("No .NET SDK could be found", message);
    }

    private static async Task WaitFor(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(60);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail("Condition not reached in time.");
            }

            await Task.Delay(20, TestContext.Current.CancellationToken);
        }
    }
}
