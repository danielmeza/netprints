using NetPrints.Editor.State;
using NetPrints.Editor.Tests.Hosting;

namespace NetPrints.Editor.Tests.State;

public sealed class StateMigrationTests : IDisposable
{
    private const UnixFileMode PrivateFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private const UnixFileMode PrivateDirectory = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    private readonly string temp = Path.Combine(Path.GetTempPath(), "netprints-migration-" + Guid.NewGuid().ToString("N"));
    private readonly CollectingLogger<StateMigration> logger = new();
    private readonly EditorDataPaths paths;

    public StateMigrationTests()
    {
        paths = new EditorDataPaths(Path.Combine(temp, "new"), legacyRoot: Path.Combine(temp, "old"));
    }

    public void Dispose()
    {
        if (!OperatingSystem.IsWindows() && Directory.Exists(temp))
        {
            foreach (string folder in Directory.EnumerateDirectories(temp, "*", SearchOption.AllDirectories).Append(temp))
            {
                File.SetUnixFileMode(folder, PrivateDirectory);
            }
        }

        if (Directory.Exists(temp))
        {
            Directory.Delete(temp, recursive: true);
        }
    }

    private string Old(params string[] parts) => Path.Combine([paths.LegacyRoot ?? string.Empty, .. parts]);

    private string New(params string[] parts) => Path.Combine([paths.Root, .. parts]);

    private static void Seed(string file, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file) ?? string.Empty);
        File.WriteAllText(file, content);
    }

    private void SeedOld()
    {
        Seed(Old("state", "recent.json"), "recent");
        Seed(Old("state", "sessions", "k.json"), "session");
        Seed(Old("backups", "key", "manifest.json"), "backup");
    }

    private StateMigration Migration(IStateFolderMover? mover = null) =>
        new(paths, new RealEditorFileSystem(), mover ?? new RealStateFolderMover(), logger);

    [Fact]
    public void StateAndBackupsMoveOnceAndNothingIsLeftBehind()
    {
        SeedOld();
        var counting = new CountingMover();

        Migration(counting).Run();

        Assert.Equal("recent", File.ReadAllText(New("state", "recent.json")));
        Assert.Equal("session", File.ReadAllText(New("state", "sessions", "k.json")));
        Assert.Equal("backup", File.ReadAllText(New("backups", "key", "manifest.json")));
        Assert.False(Directory.Exists(Old()));
        Assert.Equal(2, counting.Moves);

        Migration(counting).Run();

        Assert.Equal(2, counting.Moves);
        Assert.Empty(logger.Entries);
    }

    [Fact]
    public void ANewFolderThatExistsWinsAndTheOldOneIsUntouched()
    {
        SeedOld();
        Seed(New("state", "recent.json"), "new recent");

        Migration().Run();

        Assert.Equal("new recent", File.ReadAllText(New("state", "recent.json")));
        Assert.False(File.Exists(New("state", "sessions", "k.json")));
        Assert.Equal("recent", File.ReadAllText(Old("state", "recent.json")));
        Assert.Equal("session", File.ReadAllText(Old("state", "sessions", "k.json")));
        Assert.Equal("backup", File.ReadAllText(New("backups", "key", "manifest.json")));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void AFailedMoveIsLoggedLeavesTheOldFoldersAndNeverThrows(int kind)
    {
        SeedOld();
        Exception failure = kind == 0 ? new IOException("disk full") : new UnauthorizedAccessException("denied");

        Exception? thrown = Record.Exception(() => Migration(new FailingMover(failure)).Run());

        Assert.Null(thrown);
        Assert.Equal("recent", File.ReadAllText(Old("state", "recent.json")));
        Assert.Equal("backup", File.ReadAllText(Old("backups", "key", "manifest.json")));
        Assert.False(Directory.Exists(New("state")));
        Assert.Equal(2, logger.Entries.Count(e => e.EventId.Id == 1204 && e.Level == Microsoft.Extensions.Logging.LogLevel.Warning && ReferenceEquals(e.Exception, failure)));
    }

    [Fact]
    public void AReadOnlyOldFolderIsLoggedAndTheEditorStarts()
    {
        if (OperatingSystem.IsWindows() || Environment.UserName == "root")
        {
            return;
        }

        SeedOld();
        File.SetUnixFileMode(Old(), UnixFileMode.UserRead | UnixFileMode.UserExecute);

        Exception? thrown = Record.Exception(() => Migration().Run());

        Assert.Null(thrown);
        Assert.True(File.Exists(Old("state", "recent.json")));
        Assert.Contains(logger.Entries, e => e.EventId.Id == 1204);
    }

    [Fact]
    public void WhenTheRenameFailsTheFolderIsCopiedVerifiedAndRemoved()
    {
        SeedOld();
        var mover = new RealStateFolderMover((_, _) => throw new IOException("cross-device"));

        Migration(mover).Run();

        Assert.Equal("session", File.ReadAllText(New("state", "sessions", "k.json")));
        Assert.Equal("backup", File.ReadAllText(New("backups", "key", "manifest.json")));
        Assert.False(Directory.Exists(Old()));
        Assert.Empty(logger.Entries);
    }

    [Fact]
    public void TheNewFoldersArePrivateOnUnix()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        SeedOld();
        foreach (string path in Directory.EnumerateFileSystemEntries(Old(), "*", SearchOption.AllDirectories))
        {
            File.SetUnixFileMode(path, Directory.Exists(path)
                ? PrivateDirectory | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute
                : PrivateFile | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
        }

        Migration().Run();

        Assert.Equal(PrivateDirectory, File.GetUnixFileMode(New()));
        foreach (string path in Directory.EnumerateFileSystemEntries(New(), "*", SearchOption.AllDirectories))
        {
            Assert.Equal(Directory.Exists(path) ? PrivateDirectory : PrivateFile, File.GetUnixFileMode(path));
        }
    }

    [Fact]
    public void NothingHappensWithoutALegacyRootOrWhenItIsTheCurrentRoot()
    {
        SeedOld();
        var counting = new CountingMover();

        new StateMigration(new EditorDataPaths(Old()), new RealEditorFileSystem(), counting, logger).Run();
        new StateMigration(new EditorDataPaths(paths.Root), new RealEditorFileSystem(), counting, logger).Run();
        new StateMigration(new EditorDataPaths(Old(), legacyRoot: Old()), new RealEditorFileSystem(), counting, logger).Run();

        Assert.Equal(0, counting.Moves);
        Assert.True(File.Exists(Old("state", "recent.json")));
    }

    private sealed class CountingMover : IStateFolderMover
    {
        private readonly RealStateFolderMover inner = new();

        public int Moves { get; private set; }

        public void Move(string source, string destination)
        {
            Moves++;
            inner.Move(source, destination);
        }
    }

    private sealed class FailingMover(Exception failure) : IStateFolderMover
    {
        public void Move(string source, string destination) => throw failure;
    }
}
