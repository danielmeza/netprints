using Microsoft.Extensions.Time.Testing;
using NetPrints.Editor.Dialogs;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Lifecycle;
using NetPrints.Editor.Shell;
using NetPrints.Editor.State;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Editor.Tests.State;

namespace NetPrints.Editor.Tests.Lifecycle;

/// <summary>The shell's project flows create, discard and flush the backups of the open project.</summary>
public sealed class BackupWiringTests : IAsyncDisposable
{
    private static readonly TimeSpan Delay = TimeSpan.FromSeconds(30);

    private readonly TestEditor editor = TestEditor.Create(TestEditor.CreateReflectionHost);
    private readonly InMemoryEditorFileSystem fs = new();
    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 10, 1, 10, 0, 0, TimeSpan.Zero));
    private readonly List<ProjectRig> rigs = [];
    private readonly List<string> cleanup = [];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask DisposeAsync()
    {
        rigs.ForEach(rig => rig.Dispose());
        cleanup.ForEach(TestPaths.TryDelete);
        await editor.DisposeAsync();
    }

    private async Task<ProjectRig> OpenEditedAsync()
    {
        EditorContext context = editor.Context with { Backups = new BackupOptions(new EditorDataPaths("state-root"), fs, time, Delay) };
        string path = TestPaths.CopyHelloWorldSample();
        cleanup.Add(path);
        var rig = new ProjectRig(context, time);
        rigs.Add(rig);
        await rig.LoadProjectAsync(path);
        ProjectSessionViewModel session = rig.Session ?? throw new InvalidOperationException("No project is open.");
        session.ContextFor(session.Project.Classes.Single()).CreateVariable();
        return rig;
    }

    [Fact]
    public async Task AnEditedProjectIsBackedUpAfterTheDelay()
    {
        await OpenEditedAsync();
        time.Advance(Delay);

        Assert.Contains(fs.Files, file => file.EndsWith("manifest.json", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AHostWithoutBackupOptionsWritesNoBackups()
    {
        string path = TestPaths.CopyHelloWorldSample();
        cleanup.Add(path);
        var rig = new ProjectRig(editor.Context, time);
        rigs.Add(rig);
        await rig.LoadProjectAsync(path);

        Assert.Null(rig.Actions.Loader.Backups);
    }

    [Fact]
    public async Task DontSaveDeletesTheBackups()
    {
        ProjectRig rig = await OpenEditedAsync();
        time.Advance(Delay);
        editor.Dialogs.UnsavedAnswer = UnloadChoice.Discard;

        Assert.True(await rig.Actions.ConfirmUnloadAsync(Token));

        Assert.Empty(fs.Files);
    }

    [Fact]
    public async Task ClosingTheProjectWritesTheBackupStillWaiting()
    {
        ProjectRig rig = await OpenEditedAsync();

        rig.Actions.Loader.CloseProject();

        Assert.Contains(fs.Files, file => file.EndsWith("manifest.json", StringComparison.Ordinal));
    }
}
