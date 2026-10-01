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

public sealed class BuildCommandsTests : SessionCommandTests
{
    public static TheoryData<ICommandHandler> Handlers() =>
        [new SaveCommandHandler(), new SaveAllCommandHandler(), new CompileCommandHandler(), new RunCommandHandler()];

    [Theory]
    [MemberData(nameof(Handlers))]
    public void NothingRunsWithoutAnOpenProject(ICommandHandler handler) =>
        Assert.False(handler.CanExecute(Shell.Context()));

    [Fact]
    public void StopIsDisabledWithoutAnOpenProject() =>
        Assert.False(new StopCommandHandler().CanExecute(Shell.Context()));

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
        CommandContext context = Shell.Context(session: session);

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
        CommandContext context = Shell.Context(session: session);

        Assert.True(handler.CanExecute(context));
        await handler.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Equal("Build succeeded", session.Project.CompilationMessage);
        Assert.Empty(Editor.Processes.Started);
    }

    [Fact]
    public async Task RunStartsTheProgramAndStopEndsItThroughTheRunToken()
    {
        ProjectSessionViewModel session = await OpenSessionAsync();
        var run = new RunCommandHandler();
        var stop = new StopCommandHandler();
        CommandContext context = Shell.Context(session: session);
        Assert.True(run.CanExecute(context));
        Assert.False(stop.CanExecute(context), "Stop is enabled only while the program runs");

        await run.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Equal(RunPhase.Running, Editor.Context.RunState.Snapshot().Phase);
        Assert.False(run.CanExecute(context), "Run and Stop share one slot");
        Assert.True(stop.CanExecute(context));
        CancellationToken token = Assert.Single(Editor.Processes.Tokens);
        Assert.False(token.IsCancellationRequested);

        await stop.ExecuteAsync(context, TestContext.Current.CancellationToken);
        Assert.True(token.IsCancellationRequested);

        Editor.Processes.RaiseExited(-1);
        Assert.False(stop.CanExecute(context));
        Assert.True(run.CanExecute(context));
        Assert.Equal(RunPhase.Exited, Editor.Context.RunState.Snapshot().Phase);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompileAndRunWaitForASaveInProgress(bool run)
    {
        List<GatedDocumentStore> stores = [];
        ProjectPersistence persistence = TestEditor.CreatePersistence(Editor.Projects, store =>
        {
            var gated = new GatedDocumentStore(store);
            stores.Add(gated);
            return gated;
        });
        ProjectSessionViewModel session = await OpenSessionAsync(Editor.Context with { Persistence = persistence });
        ClassGraph cls = session.Project.Classes.Single();
        File.Delete(session.Project.GetGraphFilePath(cls));
        cls.MarkDirty();
        CommandContext context = Shell.Context(session: session);
        ICommandHandler handler = run ? new RunCommandHandler() : new CompileCommandHandler();

        Task save = new SaveCommandHandler().ExecuteAsync(context, TestContext.Current.CancellationToken);
        await stores.Single().FirstWriteStarted.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Task build = handler.ExecuteAsync(context, TestContext.Current.CancellationToken);
        try
        {
            await Task.Delay(300, TestContext.Current.CancellationToken);

            Assert.False(build.IsCompleted);
            Assert.Equal(1, stores.Sum(store => store.WriteCalls));
            Assert.Empty(Editor.Processes.Started);
        }
        finally
        {
            stores.ForEach(store => store.Release());
        }

        await save;
        await build;
        Assert.Equal(run ? 1 : 0, Editor.Processes.Started.Count);
    }
}
