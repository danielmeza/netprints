using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NetPrints.Editor.Lifecycle;
using NetPrints.Editor.State;
using NetPrints.Editor.Tests.State;

namespace NetPrints.Editor.Tests.Lifecycle;

public sealed class BackupServiceTests : IDisposable
{
    private const string ProjectPath = "projects/Hello/Hello.csproj";
    private static readonly TimeSpan Delay = TimeSpan.FromSeconds(30);

    private readonly InMemoryEditorFileSystem fs = new();
    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 10, 1, 10, 0, 0, TimeSpan.Zero));
    private readonly EditorDataPaths paths = new("state-root");
    private readonly List<string> warnings = [];
    private readonly BackupService service;

    public BackupServiceTests()
    {
        fs.Now = time.GetUtcNow().UtcDateTime;
        service = new BackupService(paths, fs, time, ProjectPath, Delay, NullLogger<BackupService>.Instance, warnings.Add);
    }

    public void Dispose() => service.Dispose();

    private string BackupFolder => paths.BackupDirectoryOf(EditorDataPaths.ProjectKey(ProjectPath));

    private string BackupFile(string original) => Path.Combine(BackupFolder, original + ".bak.json");

    private static Func<Task<byte[]?>> Content(string text) => () => Task.FromResult<byte[]?>(Encoding.UTF8.GetBytes(text));

    private void Tick(TimeSpan by)
    {
        time.Advance(by);
        fs.Now = time.GetUtcNow().UtcDateTime;
    }

    [Fact]
    public void ABackupIsWrittenThirtySecondsAfterTheLastChange()
    {
        service.Schedule("Program.netpc.json", Content("v1"));

        Tick(Delay - TimeSpan.FromMilliseconds(1));
        Assert.False(fs.FileExists(BackupFile("Program.netpc.json")));

        Tick(TimeSpan.FromMilliseconds(1));
        Assert.Equal("v1", fs.ReadText(BackupFile("Program.netpc.json")));
    }

    [Fact]
    public void EveryChangeRestartsTheWaitAndTheLatestContentIsWritten()
    {
        string current = "v1";
        Task<byte[]?> Render() => Task.FromResult<byte[]?>(Encoding.UTF8.GetBytes(current));

        service.Schedule("Program.netpc.json", Render);
        Tick(TimeSpan.FromSeconds(20));
        current = "v2";
        service.Schedule("Program.netpc.json", Render);
        Tick(TimeSpan.FromSeconds(20));
        Assert.False(fs.FileExists(BackupFile("Program.netpc.json")));

        Tick(TimeSpan.FromSeconds(10));
        Assert.Equal("v2", fs.ReadText(BackupFile("Program.netpc.json")));
    }

    [Fact]
    public void TheManifestListsEachBackupWithItsPathTimeAndHash()
    {
        service.Schedule("Program.netpc.json", Content("v1"));
        service.Schedule("sub/Other.netpc.json", Content("o1"));
        Tick(Delay);

        using JsonDocument manifest = JsonDocument.Parse(fs.ReadAllBytes(Path.Combine(BackupFolder, "manifest.json")));
        JsonElement root = manifest.RootElement;
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(ProjectPath, root.GetProperty("projectPath").GetString());
        JsonElement program = root.GetProperty("files").EnumerateArray().Single(file => file.GetProperty("originalPath").GetString() == "Program.netpc.json");
        Assert.Equal("Program.netpc.json.bak.json", program.GetProperty("backupPath").GetString());
        Assert.Equal("2026-10-01T10:00:30Z", program.GetProperty("writtenUtc").GetString());
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData("v1"u8)), program.GetProperty("sha256").GetString());
        Assert.Equal(2, root.GetProperty("files").GetArrayLength());
        Assert.True(fs.FileExists(BackupFile(Path.Combine("sub", "Other.netpc.json"))));
    }

    [Fact]
    public void NothingIsWrittenOutsideTheBackupFolderNoTmpFilesRemain()
    {
        service.Schedule("Program.netpc.json", Content("v1"));
        Tick(Delay);

        Assert.All(fs.Files, file => Assert.StartsWith(paths.Root, file, StringComparison.Ordinal));
        Assert.DoesNotContain(fs.Files, file => file.EndsWith(".tmp", StringComparison.Ordinal));
    }

    [Fact]
    public void ACancelledRenderWritesNothing()
    {
        service.Schedule("Program.netpc.json", () => Task.FromResult<byte[]?>(null));
        Tick(Delay);

        Assert.Empty(fs.Files);
    }

    [Fact]
    public void DeletingARemovesItsBackupAndPendingWaitAndTheFolderWhenNoneIsLeft()
    {
        service.Schedule("A.netpc.json", Content("a"));
        service.Schedule("B.netpc.json", Content("b"));
        Tick(Delay);

        service.Delete("A.netpc.json");
        Assert.False(fs.FileExists(BackupFile("A.netpc.json")));
        Assert.True(fs.FileExists(BackupFile("B.netpc.json")));
        Assert.Single(service.List());

        service.Schedule("A.netpc.json", Content("a2"));
        service.Delete("A.netpc.json");
        service.Delete("B.netpc.json");
        Tick(Delay);

        Assert.False(fs.DirectoryExists(BackupFolder));
        Assert.Empty(fs.Files);
    }

    [Fact]
    public void DeleteAllRemovesTheProjectFolder()
    {
        service.Schedule("A.netpc.json", Content("a"));
        service.Schedule("B.netpc.json", Content("b"));
        Tick(Delay);

        service.DeleteAll();

        Assert.False(fs.DirectoryExists(BackupFolder));
    }

    [Fact]
    public async Task FlushWritesPendingBackupsAtOnce()
    {
        service.Schedule("A.netpc.json", Content("a"));

        await service.FlushAsync();

        Assert.Equal("a", fs.ReadText(BackupFile("A.netpc.json")));
        Tick(Delay);
        Assert.Single(service.List());
    }

    [Fact]
    public void ANewSessionKeepsTheBackupsOfAnEarlierOne()
    {
        service.Schedule("A.netpc.json", Content("a"));
        Tick(Delay);

        using var second = new BackupService(paths, fs, time, ProjectPath, Delay, NullLogger<BackupService>.Instance, warnings.Add);
        second.Schedule("B.netpc.json", Content("b"));
        Tick(Delay);

        Assert.Equal(["A.netpc.json", "B.netpc.json"], second.List().Select(entry => entry.OriginalPath).Order());
        Assert.Equal(Encoding.UTF8.GetBytes("a"), second.Read(second.List().Single(entry => entry.OriginalPath == "A.netpc.json")));
    }

    [Fact]
    public void AFailingWriteIsLoggedAndWarnsOncePerSession()
    {
        fs.BeforeWrite = _ => throw new IOException("disk full");

        service.Schedule("A.netpc.json", Content("a"));
        Tick(Delay);
        service.Schedule("A.netpc.json", Content("a2"));
        Tick(Delay);

        Assert.Equal(["Backups are failing; see the log"], warnings);

        fs.BeforeWrite = null;
        service.Schedule("A.netpc.json", Content("a3"));
        Tick(Delay);
        Assert.Equal("a3", fs.ReadText(BackupFile("A.netpc.json")));
        Assert.Single(warnings);
    }

    [Fact]
    public void ABackupPathThatLeavesTheFolderIsKeptInside()
    {
        service.Schedule("../Outside.netpc.json", Content("x"));
        Tick(Delay);

        Assert.All(fs.Files, file => Assert.StartsWith(BackupFolder, file, StringComparison.Ordinal));
        BackupEntry entry = Assert.Single(service.List());
        Assert.Equal("../Outside.netpc.json", entry.OriginalPath);
        Assert.Equal(Encoding.UTF8.GetBytes("x"), service.Read(entry));
    }

    [Fact]
    public void StartupRemovesBackupsOlderThanThirtyDaysAndFoldersLeftEmpty()
    {
        fs.CreateDirectory("projects/Hello");
        fs.WriteAllBytes(ProjectPath, []);
        service.Schedule("Old.netpc.json", Content("old"));
        Tick(Delay);
        Tick(TimeSpan.FromDays(29));
        service.Schedule("Fresh.netpc.json", Content("fresh"));
        Tick(Delay);
        Tick(TimeSpan.FromDays(1));

        BackupService.CleanUp(paths, fs, time, NullLogger<BackupService>.Instance);

        BackupEntry kept = Assert.Single(service.List());
        Assert.Equal("Fresh.netpc.json", kept.OriginalPath);
        Assert.False(fs.FileExists(BackupFile("Old.netpc.json")));

        Tick(TimeSpan.FromDays(30));
        BackupService.CleanUp(paths, fs, time, NullLogger<BackupService>.Instance);
        Assert.False(fs.DirectoryExists(BackupFolder));
    }

    [Fact]
    public void StartupRemovesTheFolderOfAProjectThatNoLongerExists()
    {
        fs.CreateDirectory("projects/Hello");
        service.Schedule("A.netpc.json", Content("a"));
        Tick(Delay);

        BackupService.CleanUp(paths, fs, time, NullLogger<BackupService>.Instance);

        Assert.False(fs.DirectoryExists(BackupFolder));
    }

    [Fact]
    public void StartupKeepsTheBackupsOfAProjectWhoseFolderIsMissing()
    {
        service.Schedule("A.netpc.json", Content("a"));
        Tick(Delay);
        Assert.False(fs.DirectoryExists("projects/Hello"));

        BackupService.CleanUp(paths, fs, time, NullLogger<BackupService>.Instance);

        Assert.True(fs.FileExists(BackupFile("A.netpc.json")));
    }

    [Fact]
    public void StartupRemovesBackupsOlderThanThirtyDaysEvenWhenTheProjectFolderIsMissing()
    {
        service.Schedule("A.netpc.json", Content("a"));
        Tick(Delay);
        Tick(TimeSpan.FromDays(31));

        BackupService.CleanUp(paths, fs, time, NullLogger<BackupService>.Instance);

        Assert.False(fs.DirectoryExists(BackupFolder));
    }

    [Fact(Timeout = 30000)]
    public async Task ADeleteAllDuringAFlushWriteDoesNotBringTheBackupBack()
    {
        var release = new TaskCompletionSource<byte[]?>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.Schedule("A.netpc.json", () => release.Task);
        Task flush = service.FlushAsync();

        service.DeleteAll();
        release.SetResult([1]);
        await flush;

        Assert.False(fs.FileExists(BackupFile("A.netpc.json")));
    }

    [Fact]
    public void ANewerSchemaManifestIsNeitherOverwrittenNorDeleted()
    {
        string manifestPath = Path.Combine(BackupFolder, "manifest.json");
        byte[] foreign = Encoding.UTF8.GetBytes("""{"SchemaVersion":2,"ProjectPath":"x","Files":[{"OriginalPath":"Program.netpc.json"}]}""");
        fs.WriteAllBytes(manifestPath, foreign);
        fs.WriteAllBytes(BackupFile("Program.netpc.json"), [1, 2, 3]);

        service.Schedule("Other.netpc.json", Content("v1"));
        Tick(Delay);
        service.Delete("Other.netpc.json");
        service.Delete("Program.netpc.json");
        service.DeleteAll();

        Assert.Equal(foreign, fs.ReadAllBytes(manifestPath));
        Assert.True(fs.FileExists(BackupFile("Program.netpc.json")));
        Assert.False(fs.FileExists(BackupFile("Other.netpc.json")));
        Assert.Equal(BackupService.FailureMessage, Assert.Single(warnings));
    }

    [Fact]
    public void StartupLeavesAFolderWithAnUnreadableManifestAlone()
    {
        string folder = paths.BackupDirectoryOf("0123456789abcdef");
        fs.CreateDirectory(folder);
        fs.WriteAllBytes(Path.Combine(folder, "manifest.json"), "not json"u8.ToArray());

        BackupService.CleanUp(paths, fs, time, NullLogger<BackupService>.Instance);

        Assert.True(fs.FileExists(Path.Combine(folder, "manifest.json")));
    }

    [Theory]
    [InlineData(null, 30_000)]
    [InlineData("", 30_000)]
    [InlineData("abc", 30_000)]
    [InlineData("-5", 30_000)]
    [InlineData("0", 30_000)]
    [InlineData("250", 250)]
    public void TheDelayComesFromTheTestOnlyVariable(string? value, int expectedMilliseconds)
    {
        TimeSpan delay = BackupService.ResolveDelay(name => name == "NETPRINTS_BACKUP_DELAY" ? value : null);

        Assert.Equal(TimeSpan.FromMilliseconds(expectedMilliseconds), delay);
    }
}
