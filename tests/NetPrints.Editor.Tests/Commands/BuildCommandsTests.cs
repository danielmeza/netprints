using NetPrints.Core;
using NetPrints.Editor.Commands;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Editor.Tests.Shell;
using NetPrints.Projects;
using NetPrints.Serialization;

namespace NetPrints.Editor.Tests.Commands;

public sealed class BuildCommandsTests : IAsyncDisposable
{
    private readonly TestEditor editor = TestEditor.Create(TestEditor.CreateReflectionHost);
    private readonly FakeShell shell = new();
    private readonly List<string> cleanup = [];
    private readonly List<ProjectSessionViewModel> sessions = [];

    public async ValueTask DisposeAsync()
    {
        sessions.ForEach(session => session.Dispose());
        cleanup.ForEach(TestPaths.TryDelete);
        await editor.DisposeAsync();
    }

    private async Task<ProjectSessionViewModel> OpenSessionAsync(EditorContext? context = null)
    {
        string path = TestPaths.CopyHelloWorldSample();
        cleanup.Add(path);
        ProjectLoadResult loaded = await editor.Persistence.LoadAsync(path, TestContext.Current.CancellationToken);
        var session = new ProjectSessionViewModel(loaded.Project, context ?? editor.Context);
        sessions.Add(session);
        return session;
    }

    public static TheoryData<ICommandHandler> Handlers() =>
        [new SaveCommandHandler(), new SaveAllCommandHandler(), new CompileCommandHandler(), new RunCommandHandler()];

    [Theory]
    [MemberData(nameof(Handlers))]
    public void NothingRunsWithoutAnOpenProject(ICommandHandler handler) =>
        Assert.False(handler.CanExecute(shell.Context()));

    [Fact]
    public void StopIsDisabledWithoutAnOpenProject() =>
        Assert.False(new StopCommandHandler().CanExecute(shell.Context()));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SaveAndSaveAllWriteTheEditedClasses(bool all)
    {
        ProjectSessionViewModel session = await OpenSessionAsync();
        ICommandHandler handler = all ? new SaveAllCommandHandler() : new SaveCommandHandler();
        ClassGraph cls = session.Project.Classes.Single();
        string graphPath = session.Project.GetGraphFilePath(cls);
        File.Delete(graphPath);
        cls.MarkDirty();
        CommandContext context = shell.Context(session: session);

        Assert.True(handler.CanExecute(context));
        await handler.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.True(File.Exists(graphPath));
        Assert.False(cls.IsDirty);
    }

    [Fact]
    public async Task CompileBuildsTheProject()
    {
        ProjectSessionViewModel session = await OpenSessionAsync();
        var handler = new CompileCommandHandler();
        CommandContext context = shell.Context(session: session);

        Assert.True(handler.CanExecute(context));
        await handler.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Equal("Build succeeded", session.Project.CompilationMessage);
        Assert.Empty(editor.Processes.Started);
    }

    [Fact]
    public async Task RunStartsTheProgramAndStopEndsItThroughTheRunToken()
    {
        ProjectSessionViewModel session = await OpenSessionAsync();
        var run = new RunCommandHandler();
        var stop = new StopCommandHandler();
        CommandContext context = shell.Context(session: session);
        Assert.True(run.CanExecute(context));
        Assert.False(stop.CanExecute(context), "Stop is enabled only while the program runs");

        await run.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Equal(RunPhase.Running, editor.Context.RunState.Snapshot().Phase);
        Assert.False(run.CanExecute(context), "Run and Stop share one slot");
        Assert.True(stop.CanExecute(context));
        CancellationToken token = Assert.Single(editor.Processes.Tokens);
        Assert.False(token.IsCancellationRequested);

        await stop.ExecuteAsync(context, TestContext.Current.CancellationToken);
        Assert.True(token.IsCancellationRequested);

        editor.Processes.RaiseExited(-1);
        Assert.False(stop.CanExecute(context));
        Assert.True(run.CanExecute(context));
        Assert.Equal(RunPhase.Exited, editor.Context.RunState.Snapshot().Phase);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompileAndRunWaitForASaveInProgress(bool run)
    {
        List<GatedDocumentStore> stores = [];
        ProjectPersistence persistence = TestEditor.CreatePersistence(editor.Projects, store =>
        {
            var gated = new GatedDocumentStore(store);
            stores.Add(gated);
            return gated;
        });
        ProjectSessionViewModel session = await OpenSessionAsync(editor.Context with { Persistence = persistence });
        ClassGraph cls = session.Project.Classes.Single();
        File.Delete(session.Project.GetGraphFilePath(cls));
        cls.MarkDirty();
        CommandContext context = shell.Context(session: session);
        ICommandHandler handler = run ? new RunCommandHandler() : new CompileCommandHandler();

        Task save = new SaveCommandHandler().ExecuteAsync(context, TestContext.Current.CancellationToken);
        await stores.Single().FirstWriteStarted.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Task build = handler.ExecuteAsync(context, TestContext.Current.CancellationToken);
        try
        {
            await Task.Delay(300, TestContext.Current.CancellationToken);

            Assert.False(build.IsCompleted);
            Assert.Equal(1, stores.Sum(store => store.WriteCalls));
            Assert.Empty(editor.Processes.Started);
        }
        finally
        {
            stores.ForEach(store => store.Release());
        }

        await save;
        await build;
        Assert.Equal(run ? 1 : 0, editor.Processes.Started.Count);
    }
}
