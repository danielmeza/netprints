using NetPrints.Core;
using NetPrints.Editor.Dialogs;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Lifecycle;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Projects;
using NetPrints.Serialization;

namespace NetPrints.Editor.Tests.Lifecycle;

/// <summary>The unsaved changes prompt before the project is unloaded (FR-018, FR-022) and the exit checks around it (contracts/shell.md section 5).</summary>
public sealed class ConfirmUnloadTests : IAsyncDisposable
{
    private const string StopTitle = "Stop running program?";

    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(10);

    private readonly TestEditor editor = TestEditor.Create(TestEditor.CreateReflectionHost);
    private readonly List<ProjectRig> rigs = [];
    private readonly List<string> cleanup = [];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask DisposeAsync()
    {
        rigs.ForEach(rig => rig.Dispose());
        cleanup.ForEach(TestPaths.TryDelete);
        await editor.DisposeAsync();
    }

    private async Task<ProjectRig> OpenAsync(EditorContext? context = null)
    {
        string path = TestPaths.CopyHelloWorldSample();
        cleanup.Add(path);
        var rig = new ProjectRig(context ?? editor.Context);
        rigs.Add(rig);
        await rig.LoadProjectAsync(path);
        return rig;
    }

    private static ClassGraph MakeDirty(ProjectRig rig)
    {
        ClassGraph cls = Assert.IsType<ProjectSessionViewModel>(rig.Session).Project.Classes.Single();
        rig.Session.ContextFor(cls).CreateVariable();
        Assert.True(cls.IsDirty);
        return cls;
    }

    [Fact]
    public async Task NothingUnsavedNeverPrompts()
    {
        ProjectRig rig = await OpenAsync();

        Assert.True(await rig.Actions.ConfirmUnloadAsync(Token));

        Assert.Empty(editor.Dialogs.UnsavedCalls);
    }

    [Fact]
    public async Task NoProjectOpenNeverPrompts()
    {
        var rig = new ProjectRig(editor.Context);
        rigs.Add(rig);

        Assert.True(await rig.Actions.ConfirmUnloadAsync(Token));

        Assert.Empty(editor.Dialogs.UnsavedCalls);
    }

    [Fact]
    public async Task CancelKeepsTheProjectAndItsChanges()
    {
        ProjectRig rig = await OpenAsync();
        ClassGraph cls = MakeDirty(rig);
        editor.Dialogs.UnsavedAnswer = UnloadChoice.Cancel;

        Assert.False(await rig.Actions.ConfirmUnloadAsync(Token));

        UnsavedFile listed = Assert.Single(Assert.Single(editor.Dialogs.UnsavedCalls));
        Assert.Equal(new UnsavedFile(rig.Session?.ClassPathOf(cls) ?? "", UnsavedFileKind.Class, cls.Name), listed);
        Assert.True(cls.IsDirty);
        Assert.Same(cls, rig.Project?.Classes.Single());
    }

    [Fact]
    public async Task SaveAllSavesThenAllowsTheUnload()
    {
        ProjectRig rig = await OpenAsync();
        ClassGraph cls = MakeDirty(rig);
        editor.Dialogs.UnsavedAnswer = UnloadChoice.Save;

        Assert.True(await rig.Actions.ConfirmUnloadAsync(Token));

        Assert.False(cls.IsDirty);
        Assert.False(rig.Session?.Unsaved.HasUnsavedFiles);
        Assert.Empty(editor.Dialogs.Errors);
    }

    [Fact]
    public async Task AFailedSaveShowsTheErrorAndKeepsTheProjectOpen()
    {
        ProjectRig rig = await OpenAsync();
        ClassGraph cls = MakeDirty(rig);
        string graphPath = rig.Project?.GetGraphFilePath(cls) ?? "";
        File.Delete(graphPath);
        Directory.CreateDirectory(graphPath);
        editor.Dialogs.UnsavedAnswer = UnloadChoice.Save;

        Assert.False(await rig.Actions.ConfirmUnloadAsync(Token));

        Assert.Single(editor.Dialogs.Errors);
        Assert.True(cls.IsDirty);
    }

    [Fact]
    public async Task DontSaveAllowsTheUnloadAndWritesNothing()
    {
        ProjectRig rig = await OpenAsync();
        ClassGraph cls = MakeDirty(rig);
        string graphPath = rig.Project?.GetGraphFilePath(cls) ?? "";
        DateTime written = File.GetLastWriteTimeUtc(graphPath);
        editor.Dialogs.UnsavedAnswer = UnloadChoice.Discard;

        Assert.True(await rig.Actions.ConfirmUnloadAsync(Token));

        Assert.Equal(written, File.GetLastWriteTimeUtc(graphPath));
        Assert.Empty(editor.Dialogs.Errors);
    }

    [Fact]
    public async Task ExitWithDontSavePromptsExactlyOnceAcrossTheWindowCloseAndShutdownRequests()
    {
        ProjectRig rig = await OpenAsync();
        MakeDirty(rig);
        editor.Dialogs.UnsavedAnswer = UnloadChoice.Discard;

        Assert.True(await rig.Actions.ConfirmExitAsync(Token));
        Assert.True(await rig.Actions.ConfirmExitAsync(Token));

        Assert.Single(editor.Dialogs.UnsavedCalls);
    }

    [Fact]
    public async Task CancellingTheExitPromptKeepsTheProjectAndTheNextExitAsksAgain()
    {
        ProjectRig rig = await OpenAsync();
        MakeDirty(rig);
        editor.Dialogs.UnsavedAnswer = UnloadChoice.Cancel;

        Assert.False(await rig.Actions.ConfirmExitAsync(Token));
        Assert.False(await rig.Actions.ConfirmExitAsync(Token));

        Assert.Equal(2, editor.Dialogs.UnsavedCalls.Count);
    }

    [Fact]
    public async Task ExitWhileTheProgramRunsAsksToStopAndCancelKeepsItRunning()
    {
        ProjectRig rig = await OpenAsync();
        await Assert.IsType<ProjectSessionViewModel>(rig.Session).RunAsync();
        Assert.True(rig.Session.IsRunning);
        editor.Dialogs.ConfirmAnswer = false;

        Assert.False(await rig.Actions.ConfirmExitAsync(Token));

        Assert.Equal(StopTitle, Assert.Single(editor.Dialogs.ConfirmCalls).Title);
        Assert.False(Assert.Single(editor.Processes.Tokens).IsCancellationRequested);
        Assert.Empty(editor.Dialogs.UnsavedCalls);
    }

    [Fact]
    public async Task ExitWhileTheProgramRunsStopsItWhenConfirmedThenAsksAboutUnsavedFiles()
    {
        ProjectRig rig = await OpenAsync();
        await Assert.IsType<ProjectSessionViewModel>(rig.Session).RunAsync();
        MakeDirty(rig);
        editor.Dialogs.ConfirmAnswer = true;
        editor.Dialogs.UnsavedAnswer = UnloadChoice.Discard;

        Assert.True(await rig.Actions.ConfirmExitAsync(Token));

        Assert.Equal(StopTitle, Assert.Single(editor.Dialogs.ConfirmCalls).Title);
        Assert.True(Assert.Single(editor.Processes.Tokens).IsCancellationRequested);
        Assert.Single(editor.Dialogs.UnsavedCalls);
    }

    [Fact]
    public async Task ExitWhileNothingRunsDoesNotAskToStop()
    {
        ProjectRig rig = await OpenAsync();

        Assert.True(await rig.Actions.ConfirmExitAsync(Token));

        Assert.Empty(editor.Dialogs.ConfirmCalls);
    }

    [Fact(Timeout = 30000)]
    public async Task ExitWhileCompilingWaitsForTheBuild()
    {
        GatedDocumentStore? gate = null;
        ProjectPersistence persistence = TestEditor.CreatePersistence(editor.Projects, store => gate = new GatedDocumentStore(store));
        editor.Projects.BuildResultFactory = _ => new BuildResult(true, [], null, "");
        ProjectRig rig = await OpenAsync(editor.Context with { Persistence = persistence });
        ProjectSessionViewModel session = Assert.IsType<ProjectSessionViewModel>(rig.Session);
        ClassGraph cls = MakeDirty(rig);
        File.Delete(session.Project.GetGraphFilePath(cls));
        Task<bool> save = session.SaveAllAsync();
        Task<bool> build = session.CompileAsync();
        Assert.NotNull(gate);
        await gate.FirstWriteStarted.WaitAsync(Bound, Token);
        Assert.True(session.IsBuilding);
        editor.Dialogs.UnsavedAnswer = UnloadChoice.Discard;

        Task<bool> exit = rig.Actions.ConfirmExitAsync(Token);
        await Task.Delay(100, Token);
        Assert.False(exit.IsCompleted, "exit waits for the build");

        gate.Release();
        Assert.True(await save.WaitAsync(Bound, Token));
        Assert.True(await build.WaitAsync(Bound, Token));
        Assert.True(await exit.WaitAsync(Bound, Token));
        Assert.False(session.IsBuilding);
    }
}
