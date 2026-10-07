using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NetPrints.Editor.State;
using NetPrints.Editor.Tests.Hosting;

namespace NetPrints.Editor.Tests.State;

public sealed class StateFileRobustnessTests
{
    private const string ProjectPath = "/src/Hello/Hello.csproj";
    private const string OddRecent = """{"schemaVersion":1,"entries":[null,{"path":"/p/A.csproj","displayName":"A","lastOpenedUtc":"2026-10-01T09:00:00Z","pinned":false}]}""";

    private readonly InMemoryEditorFileSystem fs = new();
    private readonly EditorDataPaths paths = new("/state-root");
    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 10, 1, 10, 0, 0, TimeSpan.Zero));
    private readonly CollectingLogger<JsonEditorStateStore> logger = new();

    private JsonEditorStateStore Store() => new(paths, fs, logger);

    private string RecentFile => Path.Combine(paths.StateDirectory, "recent.json");

    private string LayoutFile => Path.Combine(paths.StateDirectory, "layout.json");

    private string WindowFile => Path.Combine(paths.StateDirectory, "window.json");

    private string StartFile => Path.Combine(paths.StateDirectory, "start.json");

    private void Write(string path, string json)
    {
        fs.CreateDirectory(Path.GetDirectoryName(path) ?? "");
        fs.WriteAllBytes(path, Encoding.UTF8.GetBytes(json));
    }

    [Fact]
    public void ARecentFileWithANullEntryStartsWithTheDefaults()
    {
        Write(RecentFile, OddRecent);

        Exception? thrown = Record.Exception(() => new RecentProjects(Store(), fs, time));

        Assert.Null(thrown);
        Assert.Empty(new RecentProjects(Store(), fs, time).List());
        Assert.Equal(2, logger.Entries.Count);
    }

    [Fact]
    public void ARecentEntryWithoutAPathOrNameIsUnreadableAndSearchDoesNotThrow()
    {
        Write(RecentFile, """{"schemaVersion":1,"entries":[{"lastOpenedUtc":"2026-10-01T09:00:00Z","pinned":false}]}""");
        var recent = new RecentProjects(Store(), fs, time);

        Exception? thrown = Record.Exception(() => recent.List("hello"));

        Assert.Null(thrown);
        Assert.Single(logger.Entries);
    }

    [Theory]
    [InlineData("""{"schemaVersion":1,"x":null,"y":1,"width":1,"height":1,"isMaximized":false}""")]
    [InlineData("""{"schemaVersion":1,"y":1,"width":1,"height":1,"isMaximized":false}""")]
    public void AWindowFileWithANullOrMissingFieldGivesTheDefaultsWithOneWarning(string json)
    {
        Write(WindowFile, json);

        Assert.Null(Store().LoadWindow());
        Assert.Single(logger.Entries);
    }

    [Theory]
    [InlineData("""{"schemaVersion":1,"engine":null,"dockLayout":null}""")]
    [InlineData("""{"schemaVersion":1}""")]
    public void ALayoutFileWithANullOrMissingEngineGivesTheDefaultsWithOneWarning(string json)
    {
        Write(LayoutFile, json);

        Assert.Null(Store().LoadLayout());
        Assert.Single(logger.Entries);
    }

    [Theory]
    [InlineData("""{"schemaVersion":1,"entries":[{"path":null,"displayName":"A","lastOpenedUtc":"2026-10-01T09:00:00Z","pinned":false}]}""")]
    [InlineData("""{"schemaVersion":1,"entries":[{"path":"/p","lastOpenedUtc":"2026-10-01T09:00:00Z","pinned":false}]}""")]
    [InlineData("""{"schemaVersion":1,"entries":null}""")]
    public void ARecentFileWithANullOrMissingFieldGivesTheDefaultsWithOneWarning(string json)
    {
        Write(RecentFile, json);

        Assert.Empty(Store().LoadRecent().Entries);
        Assert.Single(logger.Entries);
    }

    [Fact]
    public void AStartFileWithANullStartupBehaviorGivesTheDefaultsWithOneWarning()
    {
        Write(StartFile, """{"schemaVersion":1,"startupBehavior":null}""");

        Assert.Null(Store().LoadStart());
        Assert.Single(logger.Entries);
    }

    [Fact]
    public void ASessionWithANullProjectPathGivesNoSessionWithOneWarning()
    {
        string path = Path.Combine(paths.SessionsDirectory, EditorDataPaths.ProjectKey(ProjectPath) + ".json");
        Write(path, """{"schemaVersion":1,"projectPath":null,"openDocuments":[],"viewports":{}}""");

        Assert.Null(Store().LoadSession(ProjectPath));
        Assert.Single(logger.Entries);
    }

    [Fact]
    public void ANewerRecentFileKeepsItsEntriesWhenThisEditorOpensAProject()
    {
        const string newer = """{"schemaVersion":2,"entries":[{"path":"/p/Pinned.csproj","displayName":"Pinned","lastOpenedUtc":"2026-09-01T09:00:00Z","pinned":true}]}""";
        Write(RecentFile, newer);
        fs.WriteAllBytes("/p/A.csproj", [1]);
        var recent = new RecentProjects(Store(), fs, time);

        recent.Record("/p/A.csproj", "A");
        recent.Pin("/p/A.csproj");

        Assert.Equal(newer, fs.ReadText(RecentFile));
    }

    [Fact]
    public void ANewerFileWithAnotherShapeIsStillRecognisedAsNewer()
    {
        const string newer = """{"schemaVersion":2,"x":"left","layout":[1,2]}""";
        Write(WindowFile, newer);
        JsonEditorStateStore store = Store();

        Assert.Null(store.LoadWindow());
        store.SaveWindow(new WindowState(StateFile.CurrentVersion, 1, 2, 3, 4, false));

        Assert.Equal(newer, fs.ReadText(WindowFile));
    }

    [Fact]
    public void ANewerLayoutAndStartFileSurviveASaveUntilTheUserChangesThem()
    {
        const string newerLayout = """{"schemaVersion":2,"engine":"dock","dockLayout":{"x":1}}""";
        const string newerStart = """{"schemaVersion":2,"whatsNewSeenVersion":"9.9.9"}""";
        Write(LayoutFile, newerLayout);
        Write(StartFile, newerStart);
        JsonEditorStateStore store = Store();
        Assert.Null(store.LoadLayout());
        Assert.Null(store.LoadStart());

        store.SaveLayout(new LayoutState(StateFile.CurrentVersion, "dock", null));
        store.SaveStart(new StartState(StateFile.CurrentVersion, "0.2.0"));

        Assert.Equal(newerLayout, fs.ReadText(LayoutFile));
        Assert.Equal(newerStart, fs.ReadText(StartFile));

        store.SaveLayout(new LayoutState(StateFile.CurrentVersion, "dock", null), userChanged: true);
        store.SaveStart(new StartState(StateFile.CurrentVersion, "0.2.0"), userChanged: true);

        Assert.Contains("\"schemaVersion\": 1", fs.ReadText(LayoutFile), StringComparison.Ordinal);
        Assert.Contains("\"schemaVersion\": 1", fs.ReadText(StartFile), StringComparison.Ordinal);
    }

    [Fact]
    public void ASkippedSaveIsLoggedOnce()
    {
        Write(WindowFile, """{"schemaVersion":2}""");
        JsonEditorStateStore store = Store();
        Assert.Null(store.LoadWindow());
        int before = logger.Entries.Count;

        store.SaveWindow(new WindowState(StateFile.CurrentVersion, 1, 2, 3, 4, false));
        store.SaveWindow(new WindowState(StateFile.CurrentVersion, 1, 2, 3, 5, false));

        Assert.Equal(before + 1, logger.Entries.Count);
    }

    [Fact]
    public void TwoEditorInstancesKeepEachOthersRecentEntries()
    {
        fs.WriteAllBytes("/p/A.csproj", [1]);
        fs.WriteAllBytes("/p/B.csproj", [1]);
        var first = new RecentProjects(Store(), fs, time);
        var second = new RecentProjects(Store(), fs, time);

        second.Record("/p/B.csproj", "B");
        time.Advance(TimeSpan.FromMinutes(1));
        first.Record("/p/A.csproj", "A");

        IReadOnlyList<RecentProject> after = new RecentProjects(Store(), fs, time).List();
        Assert.Equal(["/p/A.csproj", "/p/B.csproj"], after.Select(entry => entry.Path).Order(StringComparer.Ordinal));
        Assert.Equal(2, first.List().Count);
    }

    [Fact]
    public void APinMadeInOneInstanceSurvivesAnOpenInAnother()
    {
        fs.WriteAllBytes("/p/A.csproj", [1]);
        fs.WriteAllBytes("/p/B.csproj", [1]);
        new RecentProjects(Store(), fs, time).Record("/p/A.csproj", "A");
        var first = new RecentProjects(Store(), fs, time);
        var second = new RecentProjects(Store(), fs, time);

        second.Pin("/p/A.csproj");
        first.Record("/p/B.csproj", "B");

        Assert.True(new RecentProjects(Store(), fs, time).List().Single(entry => entry.Path == "/p/A.csproj").Pinned);
    }

    [Fact]
    public void ARemoveInOneInstanceIsNotUndoneByAnotherInstancesPin()
    {
        fs.WriteAllBytes("/p/A.csproj", [1]);
        fs.WriteAllBytes("/p/B.csproj", [1]);
        var seed = new RecentProjects(Store(), fs, time);
        seed.Record("/p/A.csproj", "A");
        seed.Record("/p/B.csproj", "B");
        var first = new RecentProjects(Store(), fs, time);
        var second = new RecentProjects(Store(), fs, time);

        second.Remove("/p/A.csproj");
        first.Pin("/p/B.csproj");

        IReadOnlyList<RecentProject> after = new RecentProjects(Store(), fs, time).List();
        Assert.Equal("/p/B.csproj", Assert.Single(after).Path);
    }

    [Fact]
    public void EachWriteUsesItsOwnTemporaryFileInTheSameFolder()
    {
        var temps = new List<string>();
        fs.BeforeMove = (source, _) => temps.Add(source);
        var writer = new AtomicFileWriter(fs);
        string target = Path.Combine("state", "layout.json");

        writer.Write(target, [1]);
        writer.Write(target, [2]);

        Assert.Equal(2, temps.Distinct().Count());
        Assert.All(temps, temp =>
        {
            Assert.Equal("state", Path.GetDirectoryName(temp));
            Assert.EndsWith(".tmp", temp, StringComparison.Ordinal);
            Assert.StartsWith(target + ".", temp, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void WritersInterleavedByTheFileSystemNeverMoveEachOthersPartialFile()
    {
        string target = Path.Combine("state", "layout.json");
        var writer = new AtomicFileWriter(fs, time);
        bool nested = false;
        fs.BeforeMove = (_, _) =>
        {
            if (!nested)
            {
                nested = true;
                writer.Write(target, [2, 2]);
            }
        };

        writer.Write(target, [1, 1]);

        Assert.Equal<byte>([1, 1], fs.ReadAllBytes(target));
        Assert.Equal([target], fs.Files);
    }

    [Fact]
    public void StrayTemporaryFilesOlderThanADayAreDeleted()
    {
        string target = Path.Combine("state", "layout.json");
        fs.CreateDirectory("state");
        fs.Now = time.GetUtcNow().UtcDateTime.AddDays(-2);
        fs.WriteAllBytes(target + ".abc123.tmp", [9]);
        fs.WriteAllBytes(target + ".tmp", [9]);
        fs.Now = time.GetUtcNow().UtcDateTime;
        fs.WriteAllBytes(target + ".fresh.tmp", [9]);

        new AtomicFileWriter(fs, time).Write(target, [1]);

        Assert.Equal([target, target + ".fresh.tmp"], fs.Files.Order(StringComparer.Ordinal));
    }
}
