using NetPrints.Core;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Editor.Main;

namespace NetPrints.Editor.Tests.Main;

public class MainEditorVMTests(TestEditor testEditor) : IDisposable
{
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

        vm.Project = Project.CreateNew("P", "N");
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
        var previous = Project.CreateNew("Previous", "Prev");
        var vm = new MainEditorVM(editor.Context, previous);

        editor.FilePicker.SaveFileAnswers.Enqueue(null);
        await vm.CreateProjectCommand.ExecuteAsync(null);

        Assert.Same(previous, vm.Project);
        Assert.Contains("MyProject.netpp", editor.FilePicker.Calls.Single());
        Assert.Contains("*.netpp", editor.FilePicker.Calls.Single());
    }

    [Fact]
    public async Task CreateProjectTakesNameFromFileAndSaves()
    {
        var editor = testEditor;
        var vm = new MainEditorVM(editor.Context);
        string dir = Track(TestPaths.CreateTempDirectory());
        string path = Path.Combine(dir, "Chosen.netpp");

        editor.FilePicker.SaveFileAnswers.Enqueue(path);
        await vm.CreateProjectCommand.ExecuteAsync(null);

        Assert.NotNull(vm.Project);
        Assert.Equal("Chosen", vm.Project.Name);
        Assert.Equal("MyNamespace", vm.Project.DefaultNamespace);
        Assert.Equal(3, vm.Project.References.Count());
        Assert.True(File.Exists(path));
        Assert.Equal("Chosen", vm.Title);
    }

    [Fact]
    public async Task OpenFailureShowsErrorAndCopiesException()
    {
        var editor = testEditor;
        var vm = new MainEditorVM(editor.Context);
        string dir = Track(TestPaths.CreateTempDirectory());
        string corrupt = Path.Combine(dir, "corrupt.netpp");
        File.WriteAllText(corrupt, "this is not a project");

        editor.FilePicker.OpenFileAnswers.Enqueue(corrupt);
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

        Assert.NotNull(vm.Project);
        Assert.Equal("HelloWorld", vm.Project.Name);
        Assert.Equal("HelloWorld.Program", vm.Project.Classes.Single().FullName);

        // More than one argument is ignored.
        var other = new MainEditorVM(editor.Context);
        await other.OpenStartupProjectAsync([path, path]);
        Assert.Null(other.Project);
    }

    [Fact]
    public async Task SavePromptsOnlyWithoutPath()
    {
        var editor = testEditor;
        string dir = Track(TestPaths.CreateTempDirectory());
        var project = Project.CreateNew("Unsaved", "N");
        var vm = new MainEditorVM(editor.Context, project);

        editor.FilePicker.SaveFileAnswers.Enqueue(Path.Combine(dir, "Unsaved.netpp"));
        Assert.True(await vm.PromptProjectSaveAsync());
        Assert.Equal(1, editor.FilePicker.Calls.Count());
        Assert.True(File.Exists(Path.Combine(dir, "Unsaved.netpp")));

        project.CreateNewClass();
        await vm.SaveProjectCommand.ExecuteAsync(null);
        Assert.Equal(1, editor.FilePicker.Calls.Count());
        Assert.True(File.Exists(Path.Combine(dir, "N.MyClass.netpc")), "classes are saved with the project");
    }

    [Fact]
    public void SettingsFollowProject()
    {
        var editor = testEditor;
        var project = Project.CreateNew("P", "N");
        var vm = new MainEditorVM(editor.Context, project);

        vm.OutputBinaryType = BinaryType.Executable;
        vm.CompilationOutput = ProjectCompilationOutput.SourceCode;

        Assert.Equal(BinaryType.Executable, project.OutputBinaryType);
        Assert.Equal(ProjectCompilationOutput.SourceCode, project.CompilationOutput);
        Assert.False(vm.CanCompileAndRun, "run needs binaries");

        vm.CompilationOutput = ProjectCompilationOutput.All;
        Assert.True(vm.CanCompileAndRun);
        Assert.True(vm.RunCommand.CanExecute(null));
    }

    [Fact(Timeout = 120000)]
    public async Task RunCompilesThenStartsProgramThroughLauncher()
    {
        var editor = testEditor;
        string path = Track(TestPaths.CopyHelloWorldSample());
        var vm = new MainEditorVM(editor.Context, Project.LoadFromPath(path));

        Assert.True(vm.CanCompileAndRun);
        await vm.RunCommand.ExecuteAsync(null);

        Assert.True(vm.Project!.LastCompilationSucceeded, string.Join("\n", vm.Project.LastCompileErrors));
        Assert.Equal("Build succeeded", vm.Project.CompilationMessage);
        Assert.Equal(1, editor.Processes.Started.Count());
        var (fileName, arguments) = editor.Processes.Started[0];
        Assert.Contains("HelloWorld.exe", fileName + " " + arguments);
    }

    [Fact(Timeout = 120000)]
    public async Task CompileReportsErrors()
    {
        var editor = testEditor;
        string dir = Track(TestPaths.CreateTempDirectory());
        var project = Project.CreateNew("Broken", "N");
        project.Path = Path.Combine(dir, "Broken.netpp");
        project.References.Add(new AssemblyReference(Path.Combine(dir, "missing.dll")));
        var vm = new MainEditorVM(editor.Context, project);

        var done = new TaskCompletionSource();
        ((System.ComponentModel.INotifyPropertyChanged)project).PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Project.IsCompiling) && !project.IsCompiling)
            {
                done.TrySetResult();
            }
        };

        vm.CompileCommand.Execute(null);
        await done.Task.WaitAsync(TimeSpan.FromSeconds(90), TestContext.Current.CancellationToken);

        // The missing reference is reported (FR-009) and the status line has the error count (PAR-32).
        Assert.True(project.LastCompileErrors.Any(e => e.Contains("missing.dll")));
        Assert.False(vm.RunCommand.CanExecute(null), "library projects cannot run");
    }

    [Fact]
    public async Task NewClassNamesAreUnique()
    {
        var editor = testEditor;
        string dir = Track(TestPaths.CreateTempDirectory());
        var project = Project.CreateNew("P", "N");
        project.Path = Path.Combine(dir, "P.netpp");
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
        string sample = Track(TestPaths.CopyHelloWorldSample());
        var project = Project.CreateNew("P", "N");
        project.Path = Path.Combine(dir, "P.netpp");
        var vm = new MainEditorVM(editor.Context, project);

        editor.FilePicker.OpenFileAnswers.Enqueue(Path.Combine(Path.GetDirectoryName(sample)!, "HelloWorld.Program.netpc"));
        await vm.AddExistingClassCommand.ExecuteAsync(null);
        Assert.Equal("HelloWorld.Program", project.Classes.Single().FullName);
        Assert.True(File.Exists(Path.Combine(dir, "HelloWorld.Program.netpc")));
        Assert.Contains("*.netpc", editor.FilePicker.Calls.Single());

        string bad = Path.Combine(dir, "Bad.netpc");
        File.WriteAllText(bad, "garbage");
        editor.FilePicker.OpenFileAnswers.Enqueue(bad);
        await vm.AddExistingClassCommand.ExecuteAsync(null);
        Assert.Equal("Failed to load existing class", editor.Dialogs.Errors.Single().Title);
    }

    [Fact]
    public void OpenClassReusesWindowAndRemoveClassClosesIt()
    {
        var editor = testEditor;
        var project = Project.LoadFromPath(Track(TestPaths.CopyHelloWorldSample()));
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
    public void ClosingMainWindowClosesAllClassWindows()
    {
        var editor = testEditor;
        var project = Project.LoadFromPath(Track(TestPaths.CopyHelloWorldSample()));
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
        var project = Project.CreateNew("P", "N");
        var vm = new MainEditorVM(editor.Context, project);

        await vm.ShowReferencesCommand.ExecuteAsync(null);

        Assert.Same(project, editor.Dialogs.ReferenceDialogs.Single().Project);
    }

    [Fact(Timeout = 120000)]
    public async Task ReflectionReloadsOnOpenReferencesChangeAndCompileEnd()
    {
        var editor = new TestEditor(new ReflectionHost(new InlineDispatcher()));
        int reloads = 0;
        editor.Reflection.Reloaded += (_, _) => reloads++;

        string path = Track(TestPaths.CopyHelloWorldSample());
        var vm = new MainEditorVM(editor.Context);
        await vm.LoadProjectAsync(path);
        await WaitFor(() => reloads >= 1);
        Assert.True(editor.Reflection.NonStaticTypes.Count > 4000, "type list refreshed");

        vm.Project!.References.Add(new SourceDirectoryReference(Path.GetDirectoryName(path)!));
        await WaitFor(() => reloads >= 2);

        vm.CompileCommand.Execute(null);
        await WaitFor(() => reloads >= 3);
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
