using System.Security.Cryptography;
using System.Text;
using NetPrints.Core;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Editor.UndoRedo;
using NetPrints.Projects;
using NetPrints.Serialization;

namespace NetPrints.Editor.Tests.Shell;

public sealed class ProjectSessionViewModelTests : IAsyncDisposable
{
    private readonly TestEditor editor = TestEditor.Create(TestEditor.CreateReflectionHost);
    private readonly List<string> cleanup = [];

    public async ValueTask DisposeAsync()
    {
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
        File.Delete(graphPath);
        cls.MarkDirty();

        Assert.True(await session.SaveAllAsync());

        Assert.True(File.Exists(graphPath));
        Assert.False(cls.IsDirty);
        Assert.Empty(editor.Dialogs.Errors);
    }

    [Fact]
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
        await stores.Single().FirstWriteStarted.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
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

        Assert.True(await save);
        Assert.True(await compile);
        Assert.Equal(1, builds);
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
}
