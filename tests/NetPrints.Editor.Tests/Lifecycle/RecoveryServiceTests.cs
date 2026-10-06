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
using NetPrints.Projects;

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
        await rig.Actions.Loader.CloseProjectAsync();

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
        await rig.Actions.Loader.CloseProjectAsync();

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
    public async Task ABackupThatIsNotAGraphFileIsNotOffered()
    {
        (string project, _, _, _) = await BackUpAnEditAsync();
        var store = new BackupStore(paths, fs, project, NullLogger.Instance);
        store.Write("Gone.txt", [1, 2, 3], time.GetUtcNow().UtcDateTime);
        store.Delete(Assert.Single(store.List(), entry => entry.OriginalPath != "Gone.txt").OriginalPath);

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

    [Fact]
    public async Task DiscardDeletesOnlyTheBackupsItOffered()
    {
        (string project, _, _, _) = await BackUpAnEditAsync();
        var store = new BackupStore(paths, fs, project, NullLogger.Instance);
        store.Write("Gone.txt", [1, 2, 3], time.GetUtcNow().UtcDateTime);
        editor.Dialogs.RecoverAnswer = RecoveryChoice.Discard;

        await OpenAsync(project);

        Assert.Equal("Gone.txt", Assert.Single(store.List()).OriginalPath);
    }

    [Fact]
    public async Task ANewClassBackedUpBeforeItsFirstSaveIsOfferedAndRestoredAsUnsaved()
    {
        string path = TestPaths.CopyHelloWorldSample();
        cleanup.Add(path);
        ProjectRig rig = await OpenAsync(path);
        ProjectSessionViewModel session = rig.Session ?? throw new InvalidOperationException("No project is open.");
        ClassGraph created = session.Project.CreateNewClass(DefaultProjectProfile.Instance);
        session.ContextFor(created).CreateVariable();
        time.Advance(Delay);
        string classPath = Assert.Single(new BackupStore(paths, fs, path, NullLogger.Instance).List(), entry => entry.OriginalPath.Contains(created.Name, StringComparison.Ordinal)).OriginalPath;
        await rig.Actions.Loader.CloseProjectAsync();
        editor.Dialogs.RecoverAnswer = RecoveryChoice.Restore;

        ProjectRig reopened = await OpenAsync(path);

        Assert.Contains(classPath, Assert.Single(editor.Dialogs.RecoverCalls).Select(file => file.Path));
        ProjectSessionViewModel restoredSession = reopened.Session ?? throw new InvalidOperationException("No project is open.");
        ClassGraph restored = restoredSession.Project.Classes.Single(cls => cls.Name == created.Name);
        Assert.True(restored.IsDirty);
        Assert.Contains(classPath, restoredSession.Unsaved.UnsavedFiles.Select(file => file.Path));
        Assert.True(await restoredSession.SaveAllAsync());
        Assert.True(File.Exists(restoredSession.Project.GetGraphFilePath(restored)));
        Assert.DoesNotContain(classPath, new BackupStore(paths, fs, path, NullLogger.Instance).List().Select(entry => entry.OriginalPath));
    }

    [Fact]
    public async Task ANewClassRenamedBeforeItsFirstSaveIsBackedUpUnderTheNameItIsSavedAs()
    {
        string path = TestPaths.CopyHelloWorldSample();
        cleanup.Add(path);
        ProjectRig rig = await OpenAsync(path);
        ProjectSessionViewModel session = rig.Session ?? throw new InvalidOperationException("No project is open.");
        ClassGraph created = session.Project.CreateNewClass(DefaultProjectProfile.Instance);
        session.ContextFor(created).CreateVariable();
        time.Advance(Delay);
        string oldName = created.Name;
        created.Name = "Renamed";
        session.ContextFor(created).CreateVariable();
        time.Advance(Delay);

        BackupEntry entry = Assert.Single(new BackupStore(paths, fs, path, NullLogger.Instance).List(), item => item.OriginalPath.Contains("Renamed", StringComparison.Ordinal));
        Assert.Equal(ProjectSessionViewModel.CurrentClassPath(session.Project, created), entry.OriginalPath);
        Assert.DoesNotContain(new BackupStore(paths, fs, path, NullLogger.Instance).List(), item => item.OriginalPath.Contains(oldName, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ARenamedNewClassSavedAndEditedAgainIsOfferedAfterACrash()
    {
        string path = TestPaths.CopyHelloWorldSample();
        cleanup.Add(path);
        ProjectRig rig = await OpenAsync(path);
        ProjectSessionViewModel session = rig.Session ?? throw new InvalidOperationException("No project is open.");
        ClassGraph created = session.Project.CreateNewClass(DefaultProjectProfile.Instance);
        session.ContextFor(created).CreateVariable();
        created.Name = "Renamed";
        Assert.True(await session.SaveAllAsync());
        session.ContextFor(created).CreateVariable();
        time.Advance(Delay);
        await rig.Actions.Loader.CloseProjectAsync();

        await OpenAsync(path);

        Assert.Contains("Renamed", Assert.Single(editor.Dialogs.RecoverCalls).Single().Path, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RestoreAppliesTheChoiceOfEachFile()
    {
        (string project, string classFile, string classPath, byte[] backup) = await BackUpAnEditAsync();
        var store = new BackupStore(paths, fs, project, NullLogger.Instance);
        store.Write("Other.netpc.json", await File.ReadAllBytesAsync(classFile, Token), time.GetUtcNow().UtcDateTime);
        editor.Dialogs.RecoverAnswer = RecoveryChoice.Restore;
        editor.Dialogs.RecoverRestorePaths = [classPath];

        ProjectRig rig = await OpenAsync(project);

        ProjectSessionViewModel session = rig.Session ?? throw new InvalidOperationException("No project is open.");
        Assert.Equal(classPath, Assert.Single(session.Unsaved.UnsavedFiles).Path);
        Assert.Equal(backup, await editor.Persistence.RenderClassAsync(session.Project.Classes.Single(), Token));
        Assert.Equal(classPath, Assert.Single(store.List()).OriginalPath);
    }

    [Fact]
    public async Task RestoringNoFileDiscardsEveryOfferedBackup()
    {
        (string project, _, _, _) = await BackUpAnEditAsync();
        editor.Dialogs.RecoverAnswer = RecoveryChoice.Restore;
        editor.Dialogs.RecoverRestorePaths = [];

        ProjectRig rig = await OpenAsync(project);

        Assert.Empty(rig.Session?.Unsaved.UnsavedFiles ?? [new UnsavedFile("", UnsavedFileKind.Class, "")]);
        Assert.Empty(new BackupStore(paths, fs, project, NullLogger.Instance).List());
    }

    [Fact]
    public async Task ADeferredBackupSurvivesDontSaveOfAnotherFile()
    {
        (string project, _, _, _) = await BackUpAnEditAsync();
        editor.Dialogs.RecoverAnswer = RecoveryChoice.Later;
        ProjectRig rig = await OpenAsync(project);
        ProjectSessionViewModel session = rig.Session ?? throw new InvalidOperationException("No project is open.");
        session.Project.CreateNewClass(DefaultProjectProfile.Instance);
        editor.Dialogs.UnsavedAnswer = UnloadChoice.Discard;

        Assert.True(await rig.Actions.ConfirmUnloadAsync(Token));
        await rig.Actions.Loader.CloseProjectAsync();

        Assert.NotEmpty(new BackupStore(paths, fs, project, NullLogger.Instance).List());
    }

    [Fact]
    public async Task AnUnreadableBackupSurvivesDontSaveOfAnotherFile()
    {
        (string project, _, string classPath, _) = await BackUpAnEditAsync();
        var store = new BackupStore(paths, fs, project, NullLogger.Instance);
        store.Write(classPath, [1, 2, 3], time.GetUtcNow().UtcDateTime);
        editor.Dialogs.RecoverAnswer = RecoveryChoice.Restore;
        ProjectRig rig = await OpenAsync(project);
        ProjectSessionViewModel session = rig.Session ?? throw new InvalidOperationException("No project is open.");
        session.Project.CreateNewClass(DefaultProjectProfile.Instance);
        editor.Dialogs.UnsavedAnswer = UnloadChoice.Discard;

        Assert.True(await rig.Actions.ConfirmUnloadAsync(Token));
        await rig.Actions.Loader.CloseProjectAsync();

        Assert.Equal(classPath, Assert.Single(store.List()).OriginalPath);
    }
}
