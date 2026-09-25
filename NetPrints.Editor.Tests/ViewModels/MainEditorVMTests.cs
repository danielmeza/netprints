using NetPrints.Core;
using NetPrints.Editor.Tests.Fakes;
using NetPrints.Editor.Main;

namespace NetPrints.Editor.Tests.ViewModels;

[TestClass]
public class MainEditorVMTests
{
    private readonly List<string> cleanup = [];

    [TestCleanup]
    public void Cleanup() => cleanup.ForEach(TestPaths.TryDelete);

    private string Track(string path)
    {
        cleanup.Add(path);
        return path;
    }

    [TestMethod]
    public void PanesAreMutuallyExclusiveAndSaveNeedsProject()
    {
        var editor = new TestEditor();
        var vm = new MainEditorVM(editor.Context);

        Assert.IsFalse(vm.IsProjectOpen);
        Assert.IsFalse(vm.SaveProjectCommand.CanExecute(null));
        Assert.IsFalse(vm.ToggleSettingsPaneCommand.CanExecute(null), "Settings are disabled without a project (PAR-07)");
        Assert.IsFalse(vm.ShowReferencesCommand.CanExecute(null), "References are disabled without a project (PAR-08)");

        vm.ToggleProjectPaneCommand.Execute(null);
        Assert.IsTrue(vm.IsProjectPaneOpen);

        vm.Project = Project.CreateNew("P", "N");
        Assert.IsTrue(vm.SaveProjectCommand.CanExecute(null));
        vm.ToggleSettingsPaneCommand.Execute(null);
        Assert.IsTrue(vm.IsSettingsPaneOpen);
        Assert.IsFalse(vm.IsProjectPaneOpen, "Project and Settings panes are exclusive (PAR-02)");

        vm.ToggleProjectPaneCommand.Execute(null);
        Assert.IsTrue(vm.IsProjectPaneOpen);
        Assert.IsFalse(vm.IsSettingsPaneOpen);
    }

    [TestMethod]
    public async Task CreateProjectCancelKeepsPreviousProject()
    {
        var editor = new TestEditor();
        var previous = Project.CreateNew("Previous", "Prev");
        var vm = new MainEditorVM(editor.Context, previous);

        editor.FilePicker.SaveFileAnswers.Enqueue(null);
        await vm.CreateProjectCommand.ExecuteAsync(null);

        Assert.AreSame(previous, vm.Project);
        StringAssert.Contains(editor.FilePicker.Calls.Single(), "MyProject.netpp");
        StringAssert.Contains(editor.FilePicker.Calls.Single(), "*.netpp");
    }

    [TestMethod]
    public async Task CreateProjectTakesNameFromFileAndSaves()
    {
        var editor = new TestEditor();
        var vm = new MainEditorVM(editor.Context);
        string dir = Track(TestPaths.CreateTempDirectory());
        string path = Path.Combine(dir, "Chosen.netpp");

        editor.FilePicker.SaveFileAnswers.Enqueue(path);
        await vm.CreateProjectCommand.ExecuteAsync(null);

        Assert.IsNotNull(vm.Project);
        Assert.AreEqual("Chosen", vm.Project.Name);
        Assert.AreEqual("MyNamespace", vm.Project.DefaultNamespace);
        Assert.HasCount(3, vm.Project.References, "default references");
        Assert.IsTrue(File.Exists(path));
        Assert.AreEqual("Chosen", vm.Title);
    }

    [TestMethod]
    public async Task OpenFailureShowsErrorAndCopiesException()
    {
        var editor = new TestEditor();
        var vm = new MainEditorVM(editor.Context);
        string dir = Track(TestPaths.CreateTempDirectory());
        string corrupt = Path.Combine(dir, "corrupt.netpp");
        File.WriteAllText(corrupt, "this is not a project");

        editor.FilePicker.OpenFileAnswers.Enqueue(corrupt);
        await vm.OpenProjectCommand.ExecuteAsync(null);

        Assert.IsNull(vm.Project);
        Assert.IsFalse(vm.IsBusy);
        Assert.HasCount(1, editor.Dialogs.Errors);
        Assert.AreEqual("Failed to load project", editor.Dialogs.Errors[0].Title);
        Assert.IsNotNull(editor.Clipboard.Text);
        StringAssert.Contains(editor.Dialogs.Errors[0].Message, editor.Clipboard.Text!);
    }

    [TestMethod]
    public async Task StartupArgumentOpensProject()
    {
        var editor = new TestEditor();
        var vm = new MainEditorVM(editor.Context);
        string path = Track(TestPaths.CopyHelloWorldSample());

        await vm.OpenStartupProjectAsync([path]);

        Assert.IsNotNull(vm.Project);
        Assert.AreEqual("HelloWorld", vm.Project.Name);
        Assert.AreEqual("HelloWorld.Program", vm.Project.Classes.Single().FullName);

        // More than one argument is ignored.
        var other = new MainEditorVM(editor.Context);
        await other.OpenStartupProjectAsync([path, path]);
        Assert.IsNull(other.Project);
    }

    [TestMethod]
    public async Task SavePromptsOnlyWithoutPath()
    {
        var editor = new TestEditor();
        string dir = Track(TestPaths.CreateTempDirectory());
        var project = Project.CreateNew("Unsaved", "N");
        var vm = new MainEditorVM(editor.Context, project);

        editor.FilePicker.SaveFileAnswers.Enqueue(Path.Combine(dir, "Unsaved.netpp"));
        Assert.IsTrue(await vm.PromptProjectSaveAsync());
        Assert.HasCount(1, editor.FilePicker.Calls);
        Assert.IsTrue(File.Exists(Path.Combine(dir, "Unsaved.netpp")));

        project.CreateNewClass();
        await vm.SaveProjectCommand.ExecuteAsync(null);
        Assert.HasCount(1, editor.FilePicker.Calls, "no second prompt once a path is set");
        Assert.IsTrue(File.Exists(Path.Combine(dir, "N.MyClass.netpc")), "classes are saved with the project");
    }

    [TestMethod]
    public void SettingsFollowProject()
    {
        var editor = new TestEditor();
        var project = Project.CreateNew("P", "N");
        var vm = new MainEditorVM(editor.Context, project);

        vm.OutputBinaryType = BinaryType.Executable;
        vm.CompilationOutput = ProjectCompilationOutput.SourceCode;

        Assert.AreEqual(BinaryType.Executable, project.OutputBinaryType);
        Assert.AreEqual(ProjectCompilationOutput.SourceCode, project.CompilationOutput);
        Assert.IsFalse(vm.CanCompileAndRun, "run needs binaries");

        vm.CompilationOutput = ProjectCompilationOutput.All;
        Assert.IsTrue(vm.CanCompileAndRun);
        Assert.IsTrue(vm.RunCommand.CanExecute(null));
    }

    [TestMethod]
    [Timeout(120000, CooperativeCancellation = true)]
    public async Task RunCompilesThenStartsProgramThroughLauncher()
    {
        var editor = new TestEditor();
        string path = Track(TestPaths.CopyHelloWorldSample());
        var vm = new MainEditorVM(editor.Context, Project.LoadFromPath(path));

        Assert.IsTrue(vm.CanCompileAndRun);
        await vm.RunCommand.ExecuteAsync(null);

        Assert.IsTrue(vm.Project!.LastCompilationSucceeded, string.Join("\n", vm.Project.LastCompileErrors));
        Assert.AreEqual("Build succeeded", vm.Project.CompilationMessage);
        Assert.HasCount(1, editor.Processes.Started);
        var (fileName, arguments) = editor.Processes.Started[0];
        StringAssert.Contains(fileName + " " + arguments, "HelloWorld.exe");
    }

    [TestMethod]
    [Timeout(120000, CooperativeCancellation = true)]
    public async Task CompileReportsErrors()
    {
        var editor = new TestEditor();
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
        await done.Task.WaitAsync(TimeSpan.FromSeconds(90));

        // The missing reference is reported (FR-009) and the status line has the error count (PAR-32).
        Assert.IsTrue(project.LastCompileErrors.Any(e => e.Contains("missing.dll")));
        Assert.IsFalse(vm.RunCommand.CanExecute(null), "library projects cannot run");
    }

    [TestMethod]
    public async Task NewClassNamesAreUnique()
    {
        var editor = new TestEditor();
        string dir = Track(TestPaths.CreateTempDirectory());
        var project = Project.CreateNew("P", "N");
        project.Path = Path.Combine(dir, "P.netpp");
        var vm = new MainEditorVM(editor.Context, project);

        await vm.NewClassCommand.ExecuteAsync(null);
        await vm.NewClassCommand.ExecuteAsync(null);

        CollectionAssert.AreEqual(new[] { "MyClass", "MyClass2" }, project.Classes.Select(c => c.Name).ToArray());
        Assert.IsTrue(project.Classes.All(c => c.Namespace == "N"));
    }

    [TestMethod]
    public async Task ExistingClassIsCopiedIntoProjectAndErrorsAreShown()
    {
        var editor = new TestEditor();
        string dir = Track(TestPaths.CreateTempDirectory());
        string sample = Track(TestPaths.CopyHelloWorldSample());
        var project = Project.CreateNew("P", "N");
        project.Path = Path.Combine(dir, "P.netpp");
        var vm = new MainEditorVM(editor.Context, project);

        editor.FilePicker.OpenFileAnswers.Enqueue(Path.Combine(Path.GetDirectoryName(sample)!, "HelloWorld.Program.netpc"));
        await vm.AddExistingClassCommand.ExecuteAsync(null);
        Assert.AreEqual("HelloWorld.Program", project.Classes.Single().FullName);
        Assert.IsTrue(File.Exists(Path.Combine(dir, "HelloWorld.Program.netpc")));
        StringAssert.Contains(editor.FilePicker.Calls.Single(), "*.netpc");

        string bad = Path.Combine(dir, "Bad.netpc");
        File.WriteAllText(bad, "garbage");
        editor.FilePicker.OpenFileAnswers.Enqueue(bad);
        await vm.AddExistingClassCommand.ExecuteAsync(null);
        Assert.AreEqual("Failed to load existing class", editor.Dialogs.Errors.Single().Title);
    }

    [TestMethod]
    public void OpenClassReusesWindowAndRemoveClassClosesIt()
    {
        var editor = new TestEditor();
        var project = Project.LoadFromPath(Track(TestPaths.CopyHelloWorldSample()));
        var vm = new MainEditorVM(editor.Context, project);
        var cls = project.Classes.Single();

        vm.OpenClassCommand.Execute(cls);
        Assert.HasCount(1, editor.Windows.Open);
        var firstEditor = editor.Windows.Open[cls];

        vm.OpenClassCommand.Execute(cls);
        Assert.AreSame(firstEditor, editor.Windows.Open[cls], "an open window is reused");
        CollectionAssert.AreEqual(new[] { cls }, editor.Windows.Activated);

        vm.RemoveClassCommand.Execute(cls);
        Assert.IsEmpty(editor.Windows.Open);
        CollectionAssert.AreEqual(new[] { cls }, editor.Windows.Closed);
        Assert.IsEmpty(project.Classes, "the class is removed (fixes the WPF defect, PAR-11)");
    }

    [TestMethod]
    public void ClosingMainWindowClosesAllClassWindows()
    {
        var editor = new TestEditor();
        var project = Project.LoadFromPath(Track(TestPaths.CopyHelloWorldSample()));
        var vm = new MainEditorVM(editor.Context, project);
        vm.OpenClassCommand.Execute(project.Classes.Single());

        vm.OnMainWindowClosed();

        Assert.IsEmpty(editor.Windows.Open);
        Assert.AreEqual(1, editor.Windows.CloseAllCount);
    }

    [TestMethod]
    public async Task ReferencesDialogOpensForProject()
    {
        var editor = new TestEditor();
        var project = Project.CreateNew("P", "N");
        var vm = new MainEditorVM(editor.Context, project);

        await vm.ShowReferencesCommand.ExecuteAsync(null);

        Assert.AreSame(project, editor.Dialogs.ReferenceDialogs.Single().Project);
    }

    [TestMethod]
    [Timeout(120000, CooperativeCancellation = true)]
    public async Task ReflectionReloadsOnOpenReferencesChangeAndCompileEnd()
    {
        var editor = new TestEditor(ownReflection: true);
        int reloads = 0;
        editor.Reflection.Reloaded += (_, _) => reloads++;

        string path = Track(TestPaths.CopyHelloWorldSample());
        var vm = new MainEditorVM(editor.Context);
        await vm.LoadProjectAsync(path);
        await WaitFor(() => reloads >= 1);
        Assert.IsGreaterThan(4000, editor.Reflection.NonStaticTypes.Count, "type list refreshed");

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

            await Task.Delay(20);
        }
    }
}
