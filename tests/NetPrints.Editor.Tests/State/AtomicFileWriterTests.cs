using NetPrints.Editor.State;

namespace NetPrints.Editor.Tests.State;

public sealed class AtomicFileWriterTests : IDisposable
{
    private readonly string directory = TestPaths.CreateTempDirectory();

    public void Dispose() => TestPaths.TryDelete(directory);

    [Fact]
    public void AWriteGoesToATmpFileThatIsRenamedOverTheTarget()
    {
        var fs = new InMemoryEditorFileSystem();
        var moves = new List<(string Source, string Destination)>();
        fs.BeforeMove = (source, destination) => moves.Add((source, destination));

        new AtomicFileWriter(fs).Write(Path.Combine("state", "window.json"), [1, 2, 3]);

        (string source, string destination) = Assert.Single(moves);
        Assert.StartsWith(Path.Combine("state", "window.json."), source, StringComparison.Ordinal);
        Assert.EndsWith(".tmp", source, StringComparison.Ordinal);
        Assert.Equal(Path.Combine("state", "window.json"), destination);
        Assert.Equal([Path.Combine("state", "window.json")], fs.Files);
        Assert.Equal<byte>([1, 2, 3], fs.ReadAllBytes(Path.Combine("state", "window.json")));
    }

    [Fact]
    public void AFailedWriteLeavesTheOldContentAndNoTmpFile()
    {
        var fs = new InMemoryEditorFileSystem();
        string target = Path.Combine("state", "window.json");
        var writer = new AtomicFileWriter(fs);
        writer.Write(target, [1]);

        fs.BeforeMove = (_, _) => throw new IOException("disk full");
        Assert.Throws<IOException>(() => writer.Write(target, [2]));

        Assert.Equal([target], fs.Files);
        Assert.Equal<byte>([1], fs.ReadAllBytes(target));
    }

    [Fact]
    public void AFailureWhileWritingTheTmpFileLeavesNothingBehind()
    {
        var fs = new InMemoryEditorFileSystem { BeforeWrite = _ => throw new IOException("denied") };

        Assert.Throws<IOException>(() => new AtomicFileWriter(fs).Write(Path.Combine("state", "a.json"), [1]));

        Assert.Empty(fs.Files);
    }

    [Fact]
    public void TheRealFileSystemWritesAndReplacesFilesCreatingTheFolder()
    {
        string target = Path.Combine(directory, "state", "sessions", "a.json");
        var writer = new AtomicFileWriter(new RealEditorFileSystem());

        writer.Write(target, "one"u8.ToArray());
        writer.Write(target, "two"u8.ToArray());

        Assert.Equal("two", File.ReadAllText(target));
        Assert.Equal([target], Directory.GetFiles(Path.GetDirectoryName(target) ?? ""));
    }

    [Fact]
    public void OnUnixFoldersAreCreated0700AndFiles0600()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        string target = Path.Combine(directory, "a", "b", "file.json");

        new AtomicFileWriter(new RealEditorFileSystem()).Write(target, [1]);

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(target));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(Path.Combine(directory, "a", "b")));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(Path.Combine(directory, "a")));
    }
}
