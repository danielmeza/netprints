using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NetPrints.Core;
using NetPrints.Editor.Commands;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Editor.UndoRedo;
using NetPrints.Projects;
using NetPrints.Serialization;

namespace NetPrints.Editor.Tests.Shell;

/// <summary>The status bar says what undo, redo, a build and a run did (contracts/commands.md section 3, FR-035).</summary>
public sealed class StatusMessagesTests : IAsyncDisposable
{
    private static readonly TimeSpan MessageLifetime = TimeSpan.FromSeconds(4);

    private readonly TestEditor editor = TestEditor.Create(TestEditor.CreateReflectionHost);
    private readonly FakeTimeProvider time = new();
    private readonly List<string> cleanup = [];
    private readonly List<ProjectSessionViewModel> sessions = [];
    private readonly List<ShellViewModel> shells = [];

    public async ValueTask DisposeAsync()
    {
        shells.ForEach(shell => shell.Dispose());
        sessions.ForEach(session => session.Dispose());
        cleanup.ForEach(TestPaths.TryDelete);
        await editor.DisposeAsync();
    }

    private sealed class NoServices : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private async Task<(ShellViewModel Shell, ProjectSessionViewModel Session, ShellCommandContextProvider Contexts)> OpenAsync()
    {
        string path = TestPaths.CopyHelloWorldSample();
        cleanup.Add(path);
        ProjectLoadResult loaded = await editor.Persistence.LoadAsync(path, TestContext.Current.CancellationToken);
        var session = new ProjectSessionViewModel(loaded.Project, editor.Context);
        sessions.Add(session);
        var registry = new ContributionRegistry(NullLogger<ContributionRegistry>.Instance);
        BuiltInContributions.Register(registry);
        registry.Freeze();
        var shell = new ShellViewModel(registry, new NoServices(), time, new InlineDispatcher()) { Session = session };
        shells.Add(shell);
        return (shell, session, new ShellCommandContextProvider(shell, new FakeShell()));
    }

    private static Task RunAsync(ICommandHandler handler, ShellCommandContextProvider contexts) =>
        handler.ExecuteAsync(contexts.Create(), TestContext.Current.CancellationToken);

    [Fact]
    public async Task UndoAndRedoNameTheActionForFourSeconds()
    {
        (ShellViewModel shell, ProjectSessionViewModel session, ShellCommandContextProvider contexts) = await OpenAsync();
        ClassGraph cls = session.Project.Classes.Single();
        session.UndoStackFor(cls).Do(new DelegateUndoableCommand("Add node", () => { }, () => { }));
        shell.TreeSelection = cls;

        await RunAsync(new UndoCommandHandler(), contexts);
        Assert.Equal("Undid: Add node", shell.StatusMessage);
        time.Advance(MessageLifetime - TimeSpan.FromSeconds(1));
        Assert.Equal("Undid: Add node", shell.StatusMessage);
        time.Advance(TimeSpan.FromSeconds(1));
        Assert.Null(shell.StatusMessage);

        await RunAsync(new RedoCommandHandler(), contexts);
        Assert.Equal("Redid: Add node", shell.StatusMessage);
        time.Advance(MessageLifetime);
        Assert.Null(shell.StatusMessage);
    }

    [Fact]
    public async Task ASuccessfulCompileSaysBuildSucceeded()
    {
        (ShellViewModel shell, _, ShellCommandContextProvider contexts) = await OpenAsync();

        await RunAsync(new CompileCommandHandler(), contexts);

        Assert.Equal("Build succeeded", shell.StatusMessage);
    }

    [Fact]
    public async Task AFailedCompileSaysHowManyErrorsItFound()
    {
        (ShellViewModel shell, _, ShellCommandContextProvider contexts) = await OpenAsync();
        editor.Projects.BuildResultFactory = _ => new BuildResult(false,
        [
            new ProjectMessage(ProjectMessageSeverity.Error, "CS0006", "first", null, null, null),
            new ProjectMessage(ProjectMessageSeverity.Error, "CS0007", "second", null, null, null),
            new ProjectMessage(ProjectMessageSeverity.Warning, "CS0008", "a warning", null, null, null),
        ], null, "");

        await RunAsync(new CompileCommandHandler(), contexts);

        Assert.Equal("Build failed: 2 error(s)", shell.StatusMessage);
    }

    [Fact]
    public async Task ABuildThatThrowsSaysBuildFailed()
    {
        (ShellViewModel shell, _, ShellCommandContextProvider contexts) = await OpenAsync();
        editor.Projects.BuildResultFactory = _ => throw new InvalidOperationException("boom");

        await RunAsync(new CompileCommandHandler(), contexts);

        Assert.Equal("Build failed", shell.StatusMessage);
    }

    [Fact]
    public async Task RunningTheProgramSaysRunningThenTheExitCode()
    {
        (ShellViewModel shell, _, ShellCommandContextProvider contexts) = await OpenAsync();

        await RunAsync(new RunCommandHandler(), contexts);
        Assert.Equal("Running…", shell.StatusMessage);

        editor.Processes.RaiseExited(3);
        Assert.Equal("Exited with code 3", shell.StatusMessage);
    }

    [Fact]
    public async Task ARunThatFailsToBuildSaysBuildFailedAndNeverRunning()
    {
        (ShellViewModel shell, _, ShellCommandContextProvider contexts) = await OpenAsync();
        editor.Projects.BuildResultFactory = _ => new BuildResult(false,
            [new ProjectMessage(ProjectMessageSeverity.Error, "CS0006", "first", null, null, null)], null, "");

        await RunAsync(new RunCommandHandler(), contexts);

        Assert.Equal("Build failed: 1 error(s)", shell.StatusMessage);
    }
}
