using System.Security.Cryptography;
using System.Text;
using NetPrints.Core;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Editor.UndoRedo;
using NetPrints.Graph;
using NetPrints.Projects;
using NetPrints.Serialization;

namespace NetPrints.Editor.Tests.Shell;

public sealed class ProjectSessionViewModelTests : IAsyncDisposable
{
    private readonly TestEditor editor = TestEditor.Create(TestEditor.CreateReflectionHost);
    private readonly List<string> cleanup = [];

    public async ValueTask DisposeAsync()
    {
        storeCreated.Dispose();
        cleanup.ForEach(TestPaths.TryDelete);
        await editor.DisposeAsync();
    }

    private async Task<Project> LoadSampleAsync()
    {
        string path = TestPaths.CopyHelloWorldSample();
        cleanup.Add(path);
        ProjectLoadResult loaded = await editor.Persistence.LoadAsync(path, TestContext.Current.CancellationToken);
        return loaded.Project;
    }

    [Fact]
    public async Task ExposesTheProjectItsFileAndAKeyOfTheFullPath()
    {
        Project project = await LoadSampleAsync();
        using var session = new ProjectSessionViewModel(project, editor.Context);

        string expected = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(project.Path))))[..16];
        Assert.Same(project, session.Project);
        Assert.Equal(project.Path, session.ProjectFilePath);
        Assert.Equal(expected, session.ProjectKey);
    }

    [Fact]
    public async Task EachClassHasItsOwnUndoStackThatStaysTheSame()
    {
        Project project = await LoadSampleAsync();
        ClassGraph other = project.CreateNewClass(DefaultProjectProfile.Instance);
        using var session = new ProjectSessionViewModel(project, editor.Context);
        ClassGraph first = project.Classes[0];

        Assert.Same(session.UndoStackFor(first), session.UndoStackFor(first));
        Assert.NotSame(session.UndoStackFor(first), session.UndoStackFor(other));
    }

    [Fact]
    public async Task AdoptedUndoStackReplacesTheOneOfTheClass()
    {
        Project project = await LoadSampleAsync();
        using var session = new ProjectSessionViewModel(project, editor.Context);
        ClassGraph cls = project.Classes[0];
        var adopted = new UndoRedoStack();

        session.UseUndoStack(cls, adopted);

        Assert.Same(adopted, session.UndoStackFor(cls));
    }

    [Fact]
    public async Task SaveAllWritesTheDirtyClassesAndCleansThem()
    {
        Project project = await LoadSampleAsync();
        using var session = new ProjectSessionViewModel(project, editor.Context);
        ClassGraph cls = project.Classes.Single();
        string graphPath = project.GetGraphFilePath(cls);
        string generatedPath = Path.Combine(Path.GetDirectoryName(graphPath) ?? "", Path.GetFileNameWithoutExtension(graphPath) + ".g.cs");
        File.Delete(graphPath);
        File.Delete(generatedPath);
        cls.MarkDirty();

        Assert.True(await session.SaveAllAsync());

        Assert.True(File.Exists(graphPath));
        Assert.True(File.Exists(generatedPath));
        Assert.False(cls.IsDirty);
        Assert.Empty(editor.Dialogs.Errors);
    }

    [Fact(Timeout = 30000)]
    public async Task CompileWaitsForASaveInProgress()
    {
        Project project = await LoadSampleAsync();
        List<GatedDocumentStore> stores = [];
        ProjectPersistence persistence = TestEditor.CreatePersistence(editor.Projects, store =>
        {
            var gated = new GatedDocumentStore(store);
            stores.Add(gated);
            return gated;
        });
        int builds = 0;
        editor.Projects.BuildResultFactory = _ =>
        {
            Interlocked.Increment(ref builds);
            return new BuildResult(true, [], null, "");
        };
        using var session = new ProjectSessionViewModel(project, editor.Context with { Persistence = persistence });
        ClassGraph cls = project.Classes.Single();
        File.Delete(project.GetGraphFilePath(cls));
        cls.MarkDirty();

        Task<bool> save = session.SaveAllAsync();
        await stores.Single().FirstWriteStarted.WaitAsync(Bound, TestContext.Current.CancellationToken);
        Task<bool> compile = session.CompileAsync();
        try
        {
            await Task.Delay(300, TestContext.Current.CancellationToken);

            Assert.False(compile.IsCompleted);
            Assert.Equal(1, stores.Sum(store => store.WriteCalls));
            Assert.Equal(0, builds);
        }
        finally
        {
            stores.ForEach(store => store.Release());
        }

        Assert.True(await save.WaitAsync(Bound, TestContext.Current.CancellationToken));
        Assert.True(await compile.WaitAsync(Bound, TestContext.Current.CancellationToken));
        Assert.Equal(1, builds);
    }

    [Fact]
    public async Task CompileReportsErrors()
    {
        string dir = TestPaths.CreateTempDirectory();
        cleanup.Add(dir);
        var project = Project.FromSnapshot(TestSnapshots.Empty("Broken", "N"));
        project.Path = Path.Combine(dir, "Broken.csproj");
        editor.Projects.BuildResultFactory = _ => new BuildResult(false,
            [new ProjectMessage(ProjectMessageSeverity.Error, "CS0006", "Metadata file 'missing.dll' could not be found", null, null, null)],
            null, "");
        using var session = new ProjectSessionViewModel(project, editor.Context);

        await session.CompileAsync();

        Assert.False(project.LastCompilationSucceeded);
        Assert.Equal("Build failed with 1 error(s)", project.CompilationMessage);
        Assert.Contains(project.LastDiagnostics, d => d.Message.Contains("missing.dll", StringComparison.Ordinal));
        Assert.False(project.CanCompileAndRun, "library projects cannot run");
    }

    [Fact]
    public async Task CompileReportsATranslationFailureAsABuildError()
    {
        Project project = await LoadSampleAsync();
        using var session = new ProjectSessionViewModel(project, editor.Context);
        var cls = project.Classes.Single();
        cls.Methods.Single().Nodes.OfType<CallMethodNode>().Single().InputDataPins.Single().UnconnectedValue = null;
        cls.MarkDirty();

        await session.CompileAsync();

        Assert.False(project.LastCompilationSucceeded);
        Assert.Equal("Build failed with 1 error(s)", project.CompilationMessage);
        Assert.Equal(cls.FullName, project.LastDiagnostics.Single().ClassFullName);
        Assert.Empty(editor.Dialogs.Errors);
    }

    [Fact]
    public async Task RunStartsTheProgramWithATokenAndStopCancelsIt()
    {
        Project project = await LoadSampleAsync();
        using var session = new ProjectSessionViewModel(project, editor.Context);
        Assert.False(session.IsRunning);

        await session.RunAsync();

        Assert.True(session.IsRunning);
        CancellationToken token = Assert.Single(editor.Processes.Tokens);
        Assert.False(token.IsCancellationRequested);

        session.Stop();

        Assert.True(token.IsCancellationRequested);
    }

    [Fact]
    public async Task IsRunningEndsWhenTheProgramExits()
    {
        Project project = await LoadSampleAsync();
        using var session = new ProjectSessionViewModel(project, editor.Context);
        await session.RunAsync();

        editor.Processes.RaiseExited(0);

        Assert.False(session.IsRunning);
        Assert.Equal(RunPhase.Exited, editor.Context.RunState.Snapshot().Phase);
    }

    [Fact]
    public async Task IsRunningRaisesAChangeWhenTheProgramStartsAndExits()
    {
        Project project = await LoadSampleAsync();
        using var session = new ProjectSessionViewModel(project, editor.Context);
        List<bool> observed = [];
        session.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ProjectSessionViewModel.IsRunning))
            {
                observed.Add(session.IsRunning);
            }
        };

        await session.RunAsync();
        editor.Processes.RaiseExited(0);

        Assert.Equal([true, false], observed);
    }

    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(10);

    private readonly SemaphoreSlim storeCreated = new(0);

    private async Task<(ProjectSessionViewModel Session, List<GatedDocumentStore> Stores, int[] Builds)> GatedSessionAsync()
    {
        Project project = await LoadSampleAsync();
        List<GatedDocumentStore> stores = [];
        ProjectPersistence persistence = TestEditor.CreatePersistence(editor.Projects, store =>
        {
            var gated = new GatedDocumentStore(store);
            stores.Add(gated);
            storeCreated.Release();
            return gated;
        });
        int[] builds = [0];
        editor.Projects.BuildResultFactory = _ =>
        {
            Interlocked.Increment(ref builds[0]);
            return new BuildResult(true, [], null, "");
        };
        return (new ProjectSessionViewModel(project, editor.Context with { Persistence = persistence }), stores, builds);
    }

    private async Task<(ProjectSessionViewModel Session, List<GatedDocumentStore> Stores, int[] Builds)> HeldSaveSessionAsync()
    {
        var held = await GatedSessionAsync();
        ClassGraph cls = held.Session.Project.Classes.Single();
        File.Delete(held.Session.Project.GetGraphFilePath(cls));
        cls.MarkDirty();
        _ = held.Session.SaveAllAsync();
        await held.Stores.Single().FirstWriteStarted.WaitAsync(Bound, TestContext.Current.CancellationToken);
        return held;
    }

    private static DelegateUndoableCommand Edit() => new("edit", () => { }, () => { });

    [Fact(Timeout = 30000)]
    public async Task SaveAllMarksTheUndoStacksSaved()
    {
        Project project = await LoadSampleAsync();
        using var session = new ProjectSessionViewModel(project, editor.Context);
        ClassGraph cls = project.Classes.Single();
        UndoRedoStack stack = session.UndoStackFor(cls);
        stack.Do(Edit());
        cls.MarkDirty();

        Assert.True(await session.SaveAllAsync().WaitAsync(Bound, TestContext.Current.CancellationToken));

        Assert.True(stack.IsAtSavedState);
    }

    [Fact(Timeout = 30000)]
    public async Task AnEditMadeDuringASaveIsNotMarkedSaved()
    {
        var (session, stores, _) = await GatedSessionAsync();
        using (session)
        {
            ClassGraph cls = session.Project.Classes.Single();
            UndoRedoStack stack = session.UndoStackFor(cls);
            File.Delete(session.Project.GetGraphFilePath(cls));
            cls.MarkDirty();
            Task<bool> save = session.SaveAllAsync();
            await stores.Single().FirstWriteStarted.WaitAsync(Bound, TestContext.Current.CancellationToken);
            stack.Do(Edit());
            stores.ForEach(store => store.Release());

            Assert.True(await save.WaitAsync(Bound, TestContext.Current.CancellationToken));

            Assert.False(stack.IsAtSavedState);
            stack.Undo();
            Assert.True(stack.IsAtSavedState);
        }
    }

    [Fact(Timeout = 30000)]
    public async Task ASaveRequestedDuringASaveRunsOnceMoreAndWritesTheLaterEdit()
    {
        var (session, stores, _) = await GatedSessionAsync();
        using (session)
        {
            ClassGraph edited = session.Project.Classes.Single();
            ClassGraph held = session.Project.CreateNewClass(DefaultProjectProfile.Instance);
            string editedPath = session.Project.GetGraphFilePath(edited);
            File.Delete(editedPath);
            held.MarkDirty();
            Task<bool> first = session.SaveAllAsync();
            await stores[0].FirstWriteStarted.WaitAsync(Bound, TestContext.Current.CancellationToken);
            edited.MarkDirty();

            Task<bool> second = session.SaveAllAsync();
            Task<bool> third = session.SaveAllAsync();
            stores[0].Release();
            await storeCreated.WaitAsync(Bound, TestContext.Current.CancellationToken);
            await storeCreated.WaitAsync(Bound, TestContext.Current.CancellationToken);
            await stores[1].FirstWriteStarted.WaitAsync(Bound, TestContext.Current.CancellationToken);
            stores[1].Release();

            Assert.True(await first.WaitAsync(Bound, TestContext.Current.CancellationToken));
            Assert.True(await second.WaitAsync(Bound, TestContext.Current.CancellationToken));
            Assert.True(await third.WaitAsync(Bound, TestContext.Current.CancellationToken));
            Assert.True(File.Exists(editedPath));
            Assert.False(edited.IsDirty);
            Assert.Equal(2, stores.Count);
        }
    }

    [Fact(Timeout = 30000)]
    public async Task ASecondCompileDuringASaveJoinsTheFirstBuild()
    {
        (ProjectSessionViewModel session, List<GatedDocumentStore> stores, int[] builds) = await HeldSaveSessionAsync();
        using (session)
        {
            Task<bool> first = session.CompileAsync();
            Task<bool> second = session.CompileAsync();
            stores.ForEach(store => store.Release());

            Assert.True(await first.WaitAsync(Bound, TestContext.Current.CancellationToken));
            Assert.True(await second.WaitAsync(Bound, TestContext.Current.CancellationToken));
            Assert.Equal(1, builds[0]);
        }
    }

    [Fact(Timeout = 30000)]
    public async Task ASecondRunDuringASaveStartsOneProgram()
    {
        (ProjectSessionViewModel session, List<GatedDocumentStore> stores, int[] builds) = await HeldSaveSessionAsync();
        using (session)
        {
            Task<bool> first = session.RunAsync();
            Task<bool> second = session.RunAsync();
            stores.ForEach(store => store.Release());

            await first.WaitAsync(Bound, TestContext.Current.CancellationToken);
            await second.WaitAsync(Bound, TestContext.Current.CancellationToken);
            Assert.Equal(1, builds[0]);
            Assert.Single(editor.Processes.Tokens);
        }
    }

    [Fact]
    public async Task RunningAgainWhileTheProgramRunsStartsNothingAndStopStillCancelsIt()
    {
        Project project = await LoadSampleAsync();
        using var session = new ProjectSessionViewModel(project, editor.Context);
        await session.RunAsync();

        Assert.False(await session.RunAsync());
        session.Stop();

        CancellationToken token = Assert.Single(editor.Processes.Tokens);
        Assert.True(token.IsCancellationRequested);
    }

    [Fact]
    public async Task RunningFromTheClassEditorGoesThroughTheSessionSoStopReachesIt()
    {
        Project project = await LoadSampleAsync();
        using var session = new ProjectSessionViewModel(project, editor.Context);
        using var classEditor = new ClassEditorViewModel(project.Classes.Single(), editor.Context) { SessionSource = () => session };

        await classEditor.RunCommand.ExecuteAsync(null);
        Assert.True(session.IsRunning);
        session.Stop();

        CancellationToken token = Assert.Single(editor.Processes.Tokens);
        Assert.True(token.IsCancellationRequested);
    }

    [Fact]
    public async Task DisposingTheSessionKillsTheRunningProgram()
    {
        Project project = await LoadSampleAsync();
        var session = new ProjectSessionViewModel(project, editor.Context);
        await session.RunAsync();
        CancellationToken token = Assert.Single(editor.Processes.Tokens);

        session.Dispose();

        Assert.True(token.IsCancellationRequested);
    }

    [Fact(Timeout = 30000)]
    public async Task IsBuildingAndTheCommandStatesFollowACompileFromItsCallToItsEnd()
    {
        var (session, stores, _) = await HeldSaveSessionAsync();
        using (session)
        {
            int pulses = 0;
            session.CommandStatesChanged += (_, _) => pulses++;
            Assert.False(session.IsBuilding);

            Task<bool> compile = session.CompileAsync();

            Assert.True(session.IsBuilding);
            Assert.True(pulses >= 1, "starting a build pulses");
            int atStart = pulses;
            stores.ForEach(store => store.Release());
            Assert.True(await compile.WaitAsync(Bound, TestContext.Current.CancellationToken));

            Assert.False(session.IsBuilding);
            Assert.True(pulses > atStart, "ending a build pulses");
        }
    }

    [Fact]
    public async Task AnEditToAnUndoHistoryPulsesTheCommandStates()
    {
        Project project = await LoadSampleAsync();
        using var session = new ProjectSessionViewModel(project, editor.Context);
        ClassGraph cls = project.Classes.Single();
        var adopted = new UndoRedoStack();
        int pulses = 0;
        session.CommandStatesChanged += (_, _) => pulses++;

        session.UndoStackFor(cls).Do(Edit());
        Assert.Equal(1, pulses);

        session.UseUndoStack(cls, adopted);
        Assert.Equal(2, pulses);
        adopted.Do(Edit());
        Assert.Equal(3, pulses);
        adopted.Undo();
        Assert.Equal(4, pulses);
    }

    [Fact]
    public async Task TheProjectCompilingPulsesTheCommandStates()
    {
        Project project = await LoadSampleAsync();
        using var session = new ProjectSessionViewModel(project, editor.Context);
        int pulses = 0;
        session.CommandStatesChanged += (_, _) => pulses++;

        project.IsCompiling = true;

        Assert.Equal(1, pulses);
    }

    [Fact]
    public async Task TheProgramExitingPulsesTheCommandStates()
    {
        Project project = await LoadSampleAsync();
        using var session = new ProjectSessionViewModel(project, editor.Context);
        await session.RunAsync();
        int pulses = 0;
        session.CommandStatesChanged += (_, _) => pulses++;

        editor.Processes.RaiseExited(0);

        Assert.Equal(1, pulses);
    }
}
