using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Editor.Lifecycle;
using NetPrints.Editor.State;
using NetPrints.Editor.Tests.State;

namespace NetPrints.Editor.Tests.Lifecycle;

public sealed class BackupStoreTests
{
    private const string ProjectPath = "/proj/P.csproj";
    private static readonly DateTime Written = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryEditorFileSystem fs = new();
    private readonly EditorDataPaths paths = new("/state-root");

    private BackupStore CreateStore() => new(paths, fs, ProjectPath, NullLogger.Instance);

    [Fact]
    public void ARootedOriginalPathStaysInsideTheBackupFolder()
    {
        BackupStore store = CreateStore();

        store.Write("/proj/other/A.netpc.json", [1, 2, 3], Written);

        Assert.All(fs.Files, file => Assert.StartsWith("/state-root", file, StringComparison.Ordinal));
        BackupEntry entry = Assert.Single(store.List());
        Assert.Equal([1, 2, 3], store.Read(entry));
    }

    [Fact]
    public void ADriveQualifiedOriginalPathStaysInsideTheBackupFolder()
    {
        BackupStore store = CreateStore();

        store.Write("D:/shared/X.netpc.json", [1], Written);

        Assert.All(fs.Files, file => Assert.StartsWith("/state-root", file, StringComparison.Ordinal));
    }

    [Fact]
    public void TwoRootedPathsThatDifferOnlyByTheirRootDoNotShareABackup()
    {
        BackupStore store = CreateStore();

        store.Write("/a/X.netpc.json", [1], Written);
        store.Write("/b/X.netpc.json", [2], Written);

        Assert.Equal(2, store.List().Select(entry => entry.BackupPath).Distinct().Count());
    }

    [Fact]
    public void ATamperedBackupPathLeavesFilesOutsideTheFolderAlone()
    {
        BackupStore store = CreateStore();
        store.Write("A.netpc.json", [1], Written);
        fs.WriteAllBytes("/state-root/x", [9]);
        var tampered = new BackupManifest(BackupStore.SchemaVersion, ProjectPath,
            [new BackupEntry("A.netpc.json", "../../x", Written, "00")]);
        fs.WriteAllBytes(Path.Combine(store.Folder, "manifest.json"), JsonSerializer.SerializeToUtf8Bytes(tampered, BackupJsonContext.Default.BackupManifest));
        fs.CreateDirectory("/state-root");

        Assert.Throws<InvalidDataException>(() => store.Delete("A.netpc.json"));

        Assert.True(fs.FileExists("/state-root/x"));
    }

    [Fact]
    public void StartupCleanUpLeavesAFileOutsideTheFolderNamedByATamperedManifest()
    {
        BackupStore store = CreateStore();
        store.Write("A.netpc.json", [1], Written);
        fs.WriteAllBytes("/state-root/x", [9]);
        fs.CreateDirectory("/proj");
        var tampered = new BackupManifest(BackupStore.SchemaVersion, ProjectPath,
            [new BackupEntry("A.netpc.json", "../../x", Written, "00")]);
        fs.WriteAllBytes(Path.Combine(store.Folder, "manifest.json"), JsonSerializer.SerializeToUtf8Bytes(tampered, BackupJsonContext.Default.BackupManifest));

        BackupService.CleanUp(paths, fs, TimeProvider.System, NullLogger<BackupService>.Instance);

        Assert.True(fs.FileExists("/state-root/x"));
    }
}
