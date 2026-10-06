using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NetPrints.Editor.Dialogs;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Lifecycle;
using NetPrints.Editor.Shell;
using NetPrints.Editor.State;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Editor.Tests.State;

namespace NetPrints.Editor.Tests.Lifecycle;

/// <summary>Opening a project that has backups offers to restore them (FR-025).</summary>
public sealed class RecoveryServiceTests : IAsyncDisposable
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

    private EditorContext Context => editor.Context with { Backups = new BackupOptions(paths, fs, time, Delay) };

    private async Task<ProjectRig> OpenAsync(string path)
    {
        var rig = new ProjectRig(Context, time);
        rigs.Add(rig);
        await rig.LoadProjectAsync(path);
        return rig;
    }

    // Edits the class of a copy of the sample, lets the backup be written, closes the project, and returns what is needed to reopen it.
    private async Task<(string Project, string ClassFile, string ClassPath, byte[] Backup)> BackUpAnEditAsync(bool diskIsOlder = true)
    {
        string path = TestPaths.CopyHelloWorldSample();
        cleanup.Add(path);
        ProjectRig rig = await OpenAsync(path);
        ProjectSessionViewModel session = rig.Session ?? throw new InvalidOperationException("No project is open.");
        var cls = session.Project.Classes.Single();
        string classFile = session.Project.GetGraphFilePath(cls);
        File.SetLastWriteTimeUtc(classFile, time.GetUtcNow().UtcDateTime.AddDays(diskIsOlder ? -1 : 1));

        session.ContextFor(cls).CreateVariable();
        time.Advance(Delay);
        rig.Actions.Loader.CloseProject();

        var store = new BackupStore(paths, fs, path, NullLogger.Instance);
        BackupEntry entry = Assert.Single(store.List());
        return (path, classFile, entry.OriginalPath, store.Read(entry));
    }

    [Fact]
    public async Task AProjectWithoutBackupsOpensWithoutTheDialog()
    {
        string path = TestPaths.CopyHelloWorldSample();
        cleanup.Add(path);

        await OpenAsync(path);

        Assert.Empty(editor.Dialogs.RecoverCalls);
    }

    [Fact]
    public async Task TheDialogListsEachBackedUpFile()
    {
        (string project, _, string classPath, _) = await BackUpAnEditAsync();

        await OpenAsync(project);

        RecoveryFile file = Assert.Single(Assert.Single(editor.Dialogs.RecoverCalls));
        Assert.Equal(classPath, file.Path);
        Assert.Equal(time.GetUtcNow().UtcDateTime, file.WrittenUtc);
        Assert.False(file.IsOlderThanFile);
    }

    [Fact]
    public async Task ABackupOlderThanItsFileIsMarked()
    {
        (string project, _, _, _) = await BackUpAnEditAsync(diskIsOlder: false);

        await OpenAsync(project);

        Assert.True(Assert.Single(Assert.Single(editor.Dialogs.RecoverCalls)).IsOlderThanFile);
    }

    [Fact]
    public async Task RestoreLoadsTheBackupAsUnsavedChangesAndKeepsIt()
    {
        (string project, _, string classPath, byte[] backup) = await BackUpAnEditAsync();
        editor.Dialogs.RecoverAnswer = RecoveryChoice.Restore;

        ProjectRig rig = await OpenAsync(project);

        ProjectSessionViewModel session = rig.Session ?? throw new InvalidOperationException("No project is open.");
        Assert.Equal(classPath, Assert.Single(session.Unsaved.UnsavedFiles).Path);
        Assert.Equal(backup, await editor.Persistence.RenderClassAsync(session.Project.Classes.Single(), Token));
        Assert.NotEmpty(new BackupStore(paths, fs, project, NullLogger.Instance).List());
    }

    [Fact]
    public async Task SavingARestoredFileWritesTheBackedUpBytesAndDeletesTheBackup()
    {
        (string project, string classFile, _, byte[] backup) = await BackUpAnEditAsync();
        Assert.NotEqual(backup, await File.ReadAllBytesAsync(classFile, Token));
        editor.Dialogs.RecoverAnswer = RecoveryChoice.Restore;
        ProjectRig rig = await OpenAsync(project);
        ProjectSessionViewModel session = rig.Session ?? throw new InvalidOperationException("No project is open.");

        Assert.True(await session.SaveAllAsync());

        Assert.Equal(backup, await File.ReadAllBytesAsync(classFile, Token));
        Assert.Empty(new BackupStore(paths, fs, project, NullLogger.Instance).List());
    }

    [Fact]
    public async Task DontSaveAfterARestoreDeletesTheBackup()
    {
        (string project, _, _, _) = await BackUpAnEditAsync();
        editor.Dialogs.RecoverAnswer = RecoveryChoice.Restore;
        ProjectRig rig = await OpenAsync(project);
        editor.Dialogs.UnsavedAnswer = UnloadChoice.Discard;

        Assert.True(await rig.Actions.ConfirmUnloadAsync(Token));

        Assert.Empty(fs.Files);
    }

    [Fact]
    public async Task DiscardDeletesTheBackupsAndLeavesTheFileAlone()
    {
        (string project, string classFile, _, _) = await BackUpAnEditAsync();
        byte[] onDisk = await File.ReadAllBytesAsync(classFile, Token);
        editor.Dialogs.RecoverAnswer = RecoveryChoice.Discard;

        ProjectRig rig = await OpenAsync(project);

        Assert.Empty(fs.Files);
        Assert.Empty(rig.Session?.Unsaved.UnsavedFiles ?? [new UnsavedFile("", UnsavedFileKind.Class, "")]);
        Assert.Equal(onDisk, await File.ReadAllBytesAsync(classFile, Token));
    }

    [Fact]
    public async Task ClosingTheDialogKeepsTheBackupsAndRestoresNothing()
    {
        (string project, _, _, _) = await BackUpAnEditAsync();
        editor.Dialogs.RecoverAnswer = RecoveryChoice.Later;

        ProjectRig rig = await OpenAsync(project);

        Assert.Empty(rig.Session?.Unsaved.UnsavedFiles ?? [new UnsavedFile("", UnsavedFileKind.Class, "")]);
        Assert.NotEmpty(new BackupStore(paths, fs, project, NullLogger.Instance).List());
    }

    [Fact]
    public async Task ABackupOfAFileTheProjectNoLongerHasIsNotOffered()
    {
        (string project, _, _, _) = await BackUpAnEditAsync();
        var store = new BackupStore(paths, fs, project, NullLogger.Instance);
        store.Write("Gone.netpc.json", [1, 2, 3], time.GetUtcNow().UtcDateTime);
        store.Delete(Assert.Single(store.List(), entry => entry.OriginalPath != "Gone.netpc.json").OriginalPath);

        await OpenAsync(project);

        Assert.Empty(editor.Dialogs.RecoverCalls);
    }

    [Fact]
    public async Task AnUnreadableBackupIsReportedAndKept()
    {
        (string project, _, string classPath, _) = await BackUpAnEditAsync();
        var store = new BackupStore(paths, fs, project, NullLogger.Instance);
        store.Write(classPath, [1, 2, 3], time.GetUtcNow().UtcDateTime);
        editor.Dialogs.RecoverAnswer = RecoveryChoice.Restore;

        ProjectRig rig = await OpenAsync(project);

        Assert.Empty(rig.Session?.Unsaved.UnsavedFiles ?? [new UnsavedFile("", UnsavedFileKind.Class, "")]);
        Assert.Contains(editor.Dialogs.Errors, error => error.Message.Contains(classPath, StringComparison.Ordinal));
        Assert.NotEmpty(store.List());
    }
}
