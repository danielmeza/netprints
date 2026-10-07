using NetPrints.Core;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.References;
using NetPrints.Editor.Shell;
using NetPrints.Extensibility.Loading;
using NetPrints.Projects;

namespace NetPrints.Editor.Tests.Hosting;

/// <summary>The project load, create, open and close flows of the shell (the former main window's view model).</summary>
public sealed class ProjectLoaderTests : IDisposable
{
    // The loader reloads the reflection host fire-and-forget whenever a project is set; a private
    // host keeps those reloads from replacing the provider the shared host serves to other tests.
    private readonly TestEditor testEditor = TestEditor.Create(TestEditor.CreateReflectionHost);
    private readonly List<string> cleanup = [];
    private readonly List<ProjectRig> rigs = [];

    public void Dispose()
    {
        rigs.ForEach(rig => rig.Dispose());
        cleanup.ForEach(TestPaths.TryDelete);
    }

    private string Track(string path)
    {
        cleanup.Add(path);
        return path;
    }

    private ProjectRig NewRig(EditorContext? context = null)
    {
        var rig = new ProjectRig(context ?? testEditor.Context);
        rigs.Add(rig);
        return rig;
    }

    [Fact]
    public async Task CreateProjectCancelKeepsPreviousProject()
    {
        TestEditor editor = testEditor;
        ProjectRig rig = NewRig();
        await rig.LoadProjectAsync(Track(TestPaths.CopyHelloWorldSample()));
        ProjectSessionViewModel previous = Assert.IsType<ProjectSessionViewModel>(rig.Session);

        await rig.Actions.NewProjectAsync(TestContext.Current.CancellationToken);

        Assert.Same(previous, rig.Session);
        Assert.Equal(1, editor.Dialogs.NewProjectCalls);
    }

    [Fact]
    public async Task CreateProjectTakesTheNameFromTheDialogAndOpensIt()
    {
        TestEditor editor = testEditor;
        ProjectRig rig = NewRig();
        string dir = Track(TestPaths.CreateTempDirectory());
        string path = Path.Combine(dir, "Chosen", "Chosen.csproj");

        editor.Dialogs.NewProjectScript = async dialog =>
        {
            dialog.Name = "Chosen";
            dialog.Location = dir;
            await dialog.CreateCommand.ExecuteAsync(null);
        };
        await rig.Actions.NewProjectAsync(TestContext.Current.CancellationToken);

        var project = rig.Project;
        Assert.NotNull(project);
        Assert.Equal("Chosen", project.Name);
        Assert.Equal("Chosen", project.DefaultNamespace);
        Assert.True(File.Exists(path));
        Assert.StartsWith("Chosen", rig.Shell.Title, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenProjectWithoutAPathAsksTheFilePicker()
    {
        string path = Track(TestPaths.CopyHelloWorldSample());
        testEditor.FilePicker.OpenFileAnswers.Enqueue(path);
        ProjectRig rig = NewRig();

        await rig.Actions.OpenProjectAsync(null, TestContext.Current.CancellationToken);

        Assert.Equal(path, rig.Project?.Path);
    }

    [Fact]
    public async Task OpenProjectWithAPathLoadsIt()
    {
        string path = Track(TestPaths.CopyHelloWorldSample());
        ProjectRig rig = NewRig();

        await rig.Actions.OpenProjectAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(path, rig.Project?.Path);
        Assert.NotNull(rig.Session);
    }

    [Fact]
    public async Task OpeningANetppShowsAMessageAndWritesNothing()
    {
        TestEditor editor = testEditor;
        ProjectRig rig = NewRig();
        string dir = Track(TestPaths.CreateTempDirectory());
        string legacy = Path.Combine(dir, "Legacy.netpp");
        File.WriteAllText(legacy, "not a real project");

        editor.FilePicker.OpenFileAnswers.Enqueue(legacy);
        await rig.Actions.OpenProjectAsync(null, TestContext.Current.CancellationToken);

        Assert.Null(rig.Project);
        Assert.Equal("Unsupported project file", editor.Dialogs.Errors.Single().Title);
        Assert.Empty(editor.Projects.LoadCalls);
    }

    [Fact]
    public async Task OpenFailureShowsErrorAndCopiesException()
    {
        TestEditor editor = testEditor;
        ProjectRig rig = NewRig();
        string dir = Track(TestPaths.CreateTempDirectory());
        string missing = Path.Combine(dir, "Missing.csproj");

        editor.FilePicker.OpenFileAnswers.Enqueue(missing);
        await rig.Actions.OpenProjectAsync(null, TestContext.Current.CancellationToken);

        Assert.Null(rig.Project);
        Assert.Equal(1, editor.Dialogs.Errors.Count());
        Assert.Equal("Failed to load project", editor.Dialogs.Errors[0].Title);
        Assert.NotNull(editor.Clipboard.Text);
        Assert.Contains(editor.Clipboard.Text, editor.Dialogs.Errors[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartupArgumentOpensProject()
    {
        ProjectRig rig = NewRig();
        string path = Track(TestPaths.CopyHelloWorldSample());

        await rig.Actions.Loader.OpenStartupProjectAsync([path]);

        var project = rig.Project;
        Assert.NotNull(project);
        Assert.Equal("HelloWorld", project.Name);
        Assert.Equal("HelloWorld.Program", project.Classes.Single().FullName);

        // More than one argument is ignored.
        ProjectRig other = NewRig();
        await other.Actions.Loader.OpenStartupProjectAsync([path, path]);
        Assert.Null(other.Project);
    }

    [Fact]
    public async Task LoadingShowsAStatusMessageAndTheLoadedProjectsName()
    {
        ProjectRig rig = NewRig();
        var messages = new List<string?>();
        rig.Shell.StatusBar.PropertyChanged += (_, _) => messages.Add(rig.Shell.StatusBar.Message);

        await rig.LoadProjectAsync(Track(TestPaths.CopyHelloWorldSample()));

        Assert.Contains(messages, message => message is not null && message.StartsWith("Loading project", StringComparison.Ordinal));
        Assert.Equal("Loaded project HelloWorld", rig.Shell.StatusBar.Message);
    }

    [Fact]
    public async Task ClosingTheProjectDropsTheSession()
    {
        ProjectRig rig = NewRig();
        await rig.LoadProjectAsync(Track(TestPaths.CopyHelloWorldSample()));

        await rig.Actions.CloseProjectAsync(TestContext.Current.CancellationToken);

        Assert.Null(rig.Session);
        Assert.Null(rig.Shell.Session);
    }

    [Fact]
    public async Task ClosingTheProjectDisposesTheSessionAndStopsItsProgram()
    {
        ProjectRig rig = NewRig();
        await rig.LoadProjectAsync(Track(TestPaths.CopyHelloWorldSample()));
        await Assert.IsType<ProjectSessionViewModel>(rig.Session).RunAsync();
        CancellationToken token = Assert.Single(testEditor.Processes.Tokens);

        await rig.Actions.CloseProjectAsync(TestContext.Current.CancellationToken);

        Assert.True(token.IsCancellationRequested);
    }

    [Fact]
    public async Task LoadingAnotherProjectReplacesTheSession()
    {
        ProjectRig rig = NewRig();
        await rig.LoadProjectAsync(Track(TestPaths.CopyHelloWorldSample()));
        ProjectSessionViewModel first = Assert.IsType<ProjectSessionViewModel>(rig.Session);

        await rig.LoadProjectAsync(Track(TestPaths.CopyHelloWorldSample()));

        Assert.NotSame(first, rig.Session);
    }

    // The first reload binds Roslyn symbols for every static member (a cold JIT and a one-time warm-up): about 2 s
    // on a developer machine, and the cold open took 46 s on the slow CI runner of issues #17 and #18. Each wait
    // is a signal from ReflectionHost.Reloaded, not a poll, with a budget that covers that case several times over.
    private static readonly TimeSpan ReloadBudget = TimeSpan.FromSeconds(150);

    [Fact(Timeout = 400_000)]
    public async Task ReflectionReloadsOnOpenAndOnReferencesChange()
    {
        var editor = TestEditor.Create(TestEditor.CreateReflectionHost);
        var reloads = new ReloadCounter();
        editor.Reflection.Reloaded += (_, _) => reloads.Increment();

        string path = Track(TestPaths.CopyHelloWorldSample());
        ProjectRig rig = NewRig(editor.Context);
        await rig.LoadProjectAsync(path);
        await reloads.WaitForAsync(1, ReloadBudget);
        Assert.True(editor.Reflection.NonStaticTypes.Count > 4000, "type list refreshed");

        var project = rig.Project;
        Assert.NotNull(project);
        using var references = new ReferenceListViewModel(project, editor.Context);
        await references.AddSourceDirectoryAsync(Path.GetDirectoryName(path) ?? "");
        await reloads.WaitForAsync(2, ReloadBudget);
    }

    [Fact]
    public async Task NoSdkShowsAnErrorDialogInsteadOfCrashing()
    {
        var noSdkProjects = new NoSdkProjectSystem();
        var noSdkContext = testEditor.Context with { Projects = noSdkProjects, Persistence = TestEditor.CreatePersistence(noSdkProjects) };
        ProjectRig rig = NewRig(noSdkContext);
        string path = Track(TestPaths.CopyHelloWorldSample());

        await rig.LoadProjectAsync(path);

        Assert.Null(rig.Project);
        var (title, message) = testEditor.Dialogs.Errors.Single();
        Assert.Equal("Failed to load project", title);
        Assert.Contains(nameof(ProjectSystemException), message, StringComparison.Ordinal);
        Assert.Contains("No .NET SDK could be found", message, StringComparison.Ordinal);
    }

    // R2-22: a rollback that fails must not hide the original load error.
    [Fact]
    public async Task ARollbackFailureDoesNotHideTheOriginalLoadError()
    {
        var faultyExtensions = new FaultyRollbackExtensionHost(testEditor.Extensions);
        var context = testEditor.Context with { Extensions = faultyExtensions };
        testEditor.Dialogs.TrustAnswer = true;
        ProjectRig rig = NewRig(context);
        CancellationToken ct = TestContext.Current.CancellationToken;

        // A: opens with its own (nonexistent, but distinct) extension folder, so the active extension folders
        // change when B opens below.
        string pathA = Track(TestPaths.CopyHelloWorldSample());
        string directoryA = Path.GetDirectoryName(pathA) ?? pathA;
        ProjectSnapshot snapshotA = await testEditor.Projects.LoadAsync(pathA, ct);
        testEditor.Projects.Seed(snapshotA with { ExtensionFolders = [Path.Combine(directoryA, "ExtA")] });
        await rig.LoadProjectAsync(pathA);
        Project projectA = rig.Project ?? throw new InvalidOperationException("No project.");

        // B: a different extension folder (so the extension load swaps A's out before B's graphs are mapped)
        // and a missing graph file (so persistence.LoadAsync throws, R2-11's setup).
        string pathB = Track(TestPaths.CopyHelloWorldSample());
        string directoryB = Path.GetDirectoryName(pathB) ?? pathB;
        ProjectSnapshot snapshotB = await testEditor.Projects.LoadAsync(pathB, ct);
        testEditor.Projects.Seed(snapshotB with
        {
            ExtensionFolders = [Path.Combine(directoryB, "ExtB")],
            GraphFiles = [Path.Combine(directoryB, "Missing.netpc.json")],
        });

        // The 3rd LoadForProjectAsync call is the rollback restoring A's folders (1: A's own load, 2:
        // B's own load, 3: the rollback); making it throw must not swallow B's original load failure.
        faultyExtensions.ThrowOnCall = 3;

        await rig.LoadProjectAsync(pathB);

        Assert.Same(projectA, rig.Project);
        Assert.Contains(testEditor.Dialogs.Errors, e => e.Title == "Failed to load project");
    }

    /// <summary>Delegates to a real <see cref="IExtensionHost"/>, except a chosen call number to
    /// <see cref="LoadForProjectAsync"/> throws - used to fail the loader's rollback deliberately.</summary>
    private sealed class FaultyRollbackExtensionHost(IExtensionHost inner) : IExtensionHost
    {
        private int calls;

        /// <summary>The 1-based call number that should throw; 0 (default) never throws.</summary>
        public int ThrowOnCall { get; set; }

        public ExtensionRegistry Current => inner.Current;

        public event EventHandler<ExtensionRegistry>? RegistryChanged
        {
            add => inner.RegistryChanged += value;
            remove => inner.RegistryChanged -= value;
        }

        public ValueTask<ExtensionRegistry> LoadForProjectAsync(IReadOnlyList<string> projectExtensionFolders, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref calls) == ThrowOnCall)
            {
                throw new InvalidOperationException("Rollback load failed (test).");
            }

            return inner.LoadForProjectAsync(projectExtensionFolders, cancellationToken);
        }

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }

    private sealed class ReloadCounter
    {
        private readonly Lock gate = new();
        private readonly List<(int Count, TaskCompletionSource Signal)> waiters = [];
        private int count;

        public void Increment()
        {
            lock (gate)
            {
                count++;
                foreach (var waiter in waiters.Where(w => count >= w.Count))
                {
                    waiter.Signal.TrySetResult();
                }
            }
        }

        public async Task WaitForAsync(int expected, TimeSpan budget)
        {
            TaskCompletionSource signal = new(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (gate)
            {
                if (count >= expected)
                {
                    return;
                }

                waiters.Add((expected, signal));
            }

            try
            {
                await signal.Task.WaitAsync(budget, TestContext.Current.CancellationToken);
            }
            catch (TimeoutException)
            {
                int seen;
                lock (gate)
                {
                    seen = count;
                }

                Assert.Fail($"Reflection reload #{expected} was not signalled within {budget.TotalSeconds:F0} s (reloads seen: {seen}); the budget covers a cold runner several times over, so the reload is stuck or missing.");
            }
        }
    }
}
