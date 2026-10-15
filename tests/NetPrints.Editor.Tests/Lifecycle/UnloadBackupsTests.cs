using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NetPrints.Core;
using NetPrints.Editor.Dialogs;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Lifecycle;
using NetPrints.Editor.Shell;
using NetPrints.Editor.State;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Editor.Tests.State;

namespace NetPrints.Editor.Tests.Lifecycle;

/// <summary>Don't save deletes the backups only when the project is actually replaced (FR-018, FR-024).</summary>
public sealed class UnloadBackupsTests : IAsyncDisposable
{
    private static readonly TimeSpan Delay = TimeSpan.FromSeconds(30);

    private readonly TestEditor editor = TestEditor.Create(TestEditor.CreateReflectionHost);
    private readonly InMemoryEditorFileSystem fs = new();
    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 10, 1, 10, 0, 0, TimeSpan.Zero));
    private readonly EditorDataPaths paths = new("state-root");
    private readonly List<ProjectRig> rigs = [];
    private readonly List<string> cleanup = [];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask DisposeAsync()
    {
        rigs.ForEach(rig => rig.Dispose());
        cleanup.ForEach(TestPaths.TryDelete);
        await editor.DisposeAsync();
    }

    private string NewSample()
    {
        string path = TestPaths.CopyHelloWorldSample();
        cleanup.Add(path);
        return path;
    }

    // Opens a copy of the sample, edits its class and lets the backup be written.
    private async Task<(ProjectRig Rig, string Project, BackupStore Store)> OpenBackedUpAsync()
    {
        string path = NewSample();
        var rig = new ProjectRig(editor.Context with { Backups = new BackupOptions(paths, fs, time, Delay) }, time);
        rigs.Add(rig);
        await rig.LoadProjectAsync(path);
        ProjectSessionViewModel session = rig.Session ?? throw new InvalidOperationException("No project is open.");
        session.ContextFor(session.Project.Classes.Single()).CreateVariable();
        time.Advance(Delay);
        var store = new BackupStore(paths, fs, path, NullLogger.Instance);
        Assert.Single(store.List());
        return (rig, path, store);
    }

    private async Task AssertProjectKeptWithBackupsAsync(ProjectRig rig, string project, BackupStore store)
    {
        Assert.Equal(project, rig.Project?.Path);
        Assert.True(rig.Session?.Project.Classes.Single().IsDirty);
        Assert.NotEmpty(store.List());
        await (rig.Actions.Loader.Backups ?? throw new InvalidOperationException("No backups.")).FlushAsync();
        Assert.NotEmpty(store.List());
    }

    [Fact]
    public async Task DontSaveThenACancelledOpenPickerKeepsTheProjectAndItsBackups()
    {
        (ProjectRig rig, string project, BackupStore store) = await OpenBackedUpAsync();
        editor.Dialogs.UnsavedAnswer = UnloadChoice.Discard;

        Assert.True(await rig.Actions.ConfirmUnloadAsync(Token));
        await rig.Actions.OpenProjectAsync(null, Token);

        await AssertProjectKeptWithBackupsAsync(rig, project, store);
    }

    [Fact]
    public async Task DontSaveThenAFailedLoadKeepsTheProjectAndItsBackups()
    {
        (ProjectRig rig, string project, BackupStore store) = await OpenBackedUpAsync();
        editor.Dialogs.UnsavedAnswer = UnloadChoice.Discard;
        editor.FilePicker.OpenFileAnswers.Enqueue(Path.Combine(NewSample(), "Missing.csproj"));

        Assert.True(await rig.Actions.ConfirmUnloadAsync(Token));
        await rig.Actions.OpenProjectAsync(null, Token);

        await AssertProjectKeptWithBackupsAsync(rig, project, store);
    }

    [Fact]
    public async Task DontSaveThenACancelledNewProjectPickerKeepsTheProjectAndItsBackups()
    {
        (ProjectRig rig, string project, BackupStore store) = await OpenBackedUpAsync();
        editor.Dialogs.UnsavedAnswer = UnloadChoice.Discard;

        Assert.True(await rig.Actions.ConfirmUnloadAsync(Token));
        await rig.Actions.NewProjectAsync(Token);

        await AssertProjectKeptWithBackupsAsync(rig, project, store);
    }

    [Fact]
    public async Task DontSaveThenOpeningAnotherProjectDeletesTheBackups()
    {
        (ProjectRig rig, _, BackupStore store) = await OpenBackedUpAsync();
        editor.Dialogs.UnsavedAnswer = UnloadChoice.Discard;
        string other = NewSample();
        editor.FilePicker.OpenFileAnswers.Enqueue(other);

        Assert.True(await rig.Actions.ConfirmUnloadAsync(Token));
        await rig.Actions.OpenProjectAsync(null, Token);

        Assert.Equal(other, rig.Project?.Path);
        Assert.Empty(store.List());
    }

    [Fact]
    public async Task DontSaveAtExitDeletesTheBackupsAtOnce()
    {
        (ProjectRig rig, _, BackupStore store) = await OpenBackedUpAsync();
        editor.Dialogs.UnsavedAnswer = UnloadChoice.Discard;

        Assert.True(await rig.Actions.ConfirmExitAsync(Token));

        Assert.Empty(store.List());
    }
}
