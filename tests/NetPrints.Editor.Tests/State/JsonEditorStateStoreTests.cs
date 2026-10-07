using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NetPrints.Editor.State;
using NetPrints.Editor.Tests.Hosting;

namespace NetPrints.Editor.Tests.State;

public sealed class JsonEditorStateStoreTests
{
    private const string ProjectPath = "/src/Hello/Hello.csproj";

    private readonly InMemoryEditorFileSystem fs = new();
    private readonly EditorDataPaths paths = new("/state-root");
    private readonly CollectingLogger<JsonEditorStateStore> logger = new();

    private JsonEditorStateStore CreateStore() => new(paths, fs, logger);

    private string WindowFile => Path.Combine(paths.StateDirectory, "window.json");

    private static WindowState SampleWindow() => new(StateFile.CurrentVersion, 120, 80, 1600, 960, false, new ScreenBounds(0, 0, 2560, 1440));

    [Fact]
    public void WindowStateRoundTrips()
    {
        JsonEditorStateStore store = CreateStore();

        store.SaveWindow(SampleWindow());

        Assert.Equal(SampleWindow(), CreateStore().LoadWindow());
    }

    [Fact]
    public void LayoutStateRoundTripsItsOpaqueLayout()
    {
        JsonEditorStateStore store = CreateStore();
        JsonElement layout = JsonDocument.Parse("""{"root":{"kind":"tools","id":"a"}}""").RootElement.Clone();

        store.SaveLayout(new LayoutState(StateFile.CurrentVersion, "dock", layout));

        LayoutState? loaded = CreateStore().LoadLayout();
        Assert.NotNull(loaded);
        Assert.Equal("dock", loaded.Engine);
        Assert.Equal("a", loaded.DockLayout?.GetProperty("root").GetProperty("id").GetString());
    }

    [Fact]
    public void RecentStateRoundTrips()
    {
        var entry = new RecentEntry("/src/Hello/Hello.csproj", "Hello", new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc), true);

        CreateStore().SaveRecent(new RecentState(StateFile.CurrentVersion, [entry]));

        Assert.Equal(entry, Assert.Single(CreateStore().LoadRecent().Entries));
    }

    [Fact]
    public void StartStateRoundTripsAndAMissingFileIsTheDefaults()
    {
        Assert.Null(CreateStore().LoadStart());

        CreateStore().SaveStart(new StartState(StateFile.CurrentVersion, "0.2.0"));

        Assert.Equal("0.2.0", CreateStore().LoadStart()?.WhatsNewSeenVersion);
    }

    [Fact]
    public void SessionStateRoundTripsAndLivesUnderTheProjectKey()
    {
        var session = new SessionState(StateFile.CurrentVersion, ProjectPath, ["a", "b"], "b", new Dictionary<string, ViewportState> { ["a"] = new(-40, 12.5, 1.25) });

        CreateStore().SaveSession(session);

        string expected = Path.Combine(paths.SessionsDirectory, EditorDataPaths.ProjectKey(ProjectPath) + ".json");
        Assert.Contains(expected, fs.Files);
        SessionState? loaded = CreateStore().LoadSession(ProjectPath);
        Assert.NotNull(loaded);
        Assert.Equal(["a", "b"], loaded.OpenDocuments);
        Assert.Equal("b", loaded.ActiveDocument);
        Assert.Equal(new ViewportState(-40, 12.5, 1.25), loaded.Viewports["a"]);
    }

    [Fact]
    public void FilesCarrySchemaVersionOneAndAreUtf8WithoutBomAndLfOnly()
    {
        JsonEditorStateStore store = CreateStore();
        store.SaveWindow(SampleWindow());
        store.SaveLayout(new LayoutState(StateFile.CurrentVersion, "dock", null));
        store.SaveRecent(RecentState.Empty);
        store.SaveSession(new SessionState(StateFile.CurrentVersion, ProjectPath, [], null, new Dictionary<string, ViewportState>()));

        Assert.Equal(4, fs.Files.Count);
        foreach (string file in fs.Files)
        {
            byte[] bytes = fs.ReadAllBytes(file);
            Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, file);
            string text = Encoding.UTF8.GetString(bytes);
            Assert.DoesNotContain('\r', text);
            Assert.Equal(1, JsonDocument.Parse(text).RootElement.GetProperty("schemaVersion").GetInt32());
        }
    }

    [Fact]
    public void WritesGoThroughATemporaryFile()
    {
        var moves = new List<string>();
        fs.BeforeMove = (source, _) => moves.Add(source);

        CreateStore().SaveWindow(SampleWindow());

        Assert.Equal(WindowFile + ".tmp", Assert.Single(moves));
        Assert.DoesNotContain(WindowFile + ".tmp", fs.Files);
    }

    [Fact]
    public void AMissingFileGivesTheDefaultsWithoutAWarning()
    {
        JsonEditorStateStore store = CreateStore();

        Assert.Null(store.LoadWindow());
        Assert.Null(store.LoadLayout());
        Assert.Empty(store.LoadRecent().Entries);
        Assert.Null(store.LoadSession(ProjectPath));
        Assert.Empty(logger.Entries);
        Assert.Empty(fs.Files);
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("""{"schemaVersion":0,"x":1,"y":1,"width":1,"height":1,"isMaximized":false}""")]
    [InlineData("""{"schemaVersion":2,"x":1,"y":1,"width":1,"height":1,"isMaximized":false}""")]
    public void AnUnreadableOrNewerFileGivesTheDefaultsWithOneWarningAndIsLeftAlone(string content)
    {
        fs.CreateDirectory(paths.StateDirectory);
        byte[] original = Encoding.UTF8.GetBytes(content);
        fs.WriteAllBytes(WindowFile, original);

        WindowState? loaded = CreateStore().LoadWindow();

        Assert.Null(loaded);
        Assert.Equal(LogLevel.Warning, Assert.Single(logger.Entries).Level);
        Assert.Equal(original, fs.ReadAllBytes(WindowFile));
        Assert.Single(fs.Files);
    }

    [Fact]
    public void AFileThatCannotBeReadGivesTheDefaultsWithOneWarning()
    {
        fs.CreateDirectory(paths.StateDirectory);
        fs.WriteAllBytes(WindowFile, [1]);
        var unreadable = new UnreadableFileSystem(fs);
        var store = new JsonEditorStateStore(paths, unreadable, logger);

        Assert.Null(store.LoadWindow());
        Assert.Equal(LogLevel.Warning, Assert.Single(logger.Entries).Level);
    }

    [Fact]
    public void ANewerFileIsReplacedOnlyWhenTheStateIsSavedAgain()
    {
        fs.CreateDirectory(paths.StateDirectory);
        fs.WriteAllBytes(WindowFile, Encoding.UTF8.GetBytes("""{"schemaVersion":9}"""));
        JsonEditorStateStore store = CreateStore();
        _ = store.LoadWindow();

        store.SaveWindow(SampleWindow());

        Assert.Equal(SampleWindow(), CreateStore().LoadWindow());
    }

    [Fact]
    public void AFailedSaveIsLoggedAndDoesNotThrow()
    {
        fs.BeforeWrite = _ => throw new IOException("disk full");

        CreateStore().SaveWindow(SampleWindow());

        Assert.Equal(LogLevel.Warning, Assert.Single(logger.Entries).Level);
        Assert.Empty(fs.Files);
    }

    private sealed class UnreadableFileSystem(InMemoryEditorFileSystem inner) : IEditorFileSystem
    {
        public bool FileExists(string path) => inner.FileExists(path);
        public bool DirectoryExists(string path) => inner.DirectoryExists(path);
        public void CreateDirectory(string path) => inner.CreateDirectory(path);
        public void WriteAllBytes(string path, byte[] bytes) => inner.WriteAllBytes(path, bytes);
        public byte[] ReadAllBytes(string path) => throw new UnauthorizedAccessException(path);
        public void Move(string source, string destination) => inner.Move(source, destination);
        public void DeleteFile(string path) => inner.DeleteFile(path);
        public void DeleteDirectory(string path) => inner.DeleteDirectory(path);
        public IEnumerable<string> EnumerateDirectories(string path) => inner.EnumerateDirectories(path);
        public IEnumerable<string> EnumerateFiles(string path) => inner.EnumerateFiles(path);
        public DateTime GetLastWriteTimeUtc(string path) => inner.GetLastWriteTimeUtc(path);
    }
}
