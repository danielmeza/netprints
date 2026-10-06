using Microsoft.Extensions.Time.Testing;
using NetPrints.Editor.State;

namespace NetPrints.Editor.Tests.State;

public sealed class RecentProjectsTests
{
    private readonly InMemoryEditorFileSystem fs = new();
    private readonly EditorDataPaths paths = new("/state-root");
    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 10, 1, 10, 0, 0, TimeSpan.Zero));

    private RecentProjects Create(StringComparison? comparison = null) =>
        new(new JsonEditorStateStore(paths, fs, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance), fs, time, comparison);

    private string Existing(string path)
    {
        fs.WriteAllBytes(path, [1]);
        return path;
    }

    private void Open(RecentProjects recent, string path, string? name = null)
    {
        recent.Record(Existing(path), name ?? Path.GetFileNameWithoutExtension(path));
        time.Advance(TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void TheMostRecentComesFirst()
    {
        RecentProjects recent = Create();
        Open(recent, "/a/A.csproj");
        Open(recent, "/b/B.csproj");

        Assert.Equal(["/b/B.csproj", "/a/A.csproj"], recent.List().Select(entry => entry.Path));
    }

    [Fact]
    public void OpeningAgainMovesAnEntryToTheFrontWithoutDuplicatingIt()
    {
        RecentProjects recent = Create();
        Open(recent, "/a/A.csproj");
        Open(recent, "/b/B.csproj");
        Open(recent, "/a/A.csproj");

        Assert.Equal(["/a/A.csproj", "/b/B.csproj"], recent.List().Select(entry => entry.Path));
    }

    [Fact]
    public void PinnedEntriesComeFirstThenTheMostRecent()
    {
        RecentProjects recent = Create();
        Open(recent, "/a/A.csproj");
        Open(recent, "/b/B.csproj");
        Open(recent, "/c/C.csproj");
        recent.Pin("/a/A.csproj");

        Assert.Equal(["/a/A.csproj", "/c/C.csproj", "/b/B.csproj"], recent.List().Select(entry => entry.Path));
        Assert.True(recent.List()[0].Pinned);

        recent.Unpin("/a/A.csproj");
        Assert.Equal(["/c/C.csproj", "/b/B.csproj", "/a/A.csproj"], recent.List().Select(entry => entry.Path));
    }

    [Fact]
    public void AtMostTwentyUnpinnedEntriesAreKeptAndTheOldestIsDropped()
    {
        RecentProjects recent = Create();
        for (int i = 0; i < 22; i++)
        {
            Open(recent, $"/p/P{i}.csproj");
        }

        IReadOnlyList<RecentProject> list = recent.List();
        Assert.Equal(20, list.Count);
        Assert.DoesNotContain(list, entry => entry.Path is "/p/P0.csproj" or "/p/P1.csproj");
        Assert.Equal("/p/P21.csproj", list[0].Path);
    }

    [Fact]
    public void PinnedEntriesAreNeverDropped()
    {
        RecentProjects recent = Create();
        Open(recent, "/p/Old.csproj");
        recent.Pin("/p/Old.csproj");
        for (int i = 0; i < 25; i++)
        {
            Open(recent, $"/p/P{i}.csproj");
        }

        IReadOnlyList<RecentProject> list = recent.List();
        Assert.Equal(21, list.Count);
        Assert.Equal("/p/Old.csproj", list[0].Path);
    }

    [Fact]
    public void ReopeningAPinnedEntryKeepsItPinned()
    {
        RecentProjects recent = Create();
        Open(recent, "/a/A.csproj");
        recent.Pin("/a/A.csproj");
        Open(recent, "/a/A.csproj");

        Assert.True(Assert.Single(recent.List()).Pinned);
    }

    [Fact]
    public void SearchMatchesNameOrPathCaseInsensitively()
    {
        RecentProjects recent = Create();
        Open(recent, "/src/Hello/Hello.csproj", "Hello");
        Open(recent, "/work/Other/Game.csproj", "Arcade");

        Assert.Equal(["/src/Hello/Hello.csproj"], recent.List("hELLo").Select(entry => entry.Path));
        Assert.Equal(["/work/Other/Game.csproj"], recent.List("arCADE").Select(entry => entry.Path));
        Assert.Equal(["/work/Other/Game.csproj"], recent.List("/WORK/").Select(entry => entry.Path));
        Assert.Equal(2, recent.List("").Count);
        Assert.Empty(recent.List("nothing"));
    }

    [Fact]
    public void AMissingPathIsFlaggedUnavailableAndKept()
    {
        RecentProjects recent = Create();
        Open(recent, "/a/A.csproj");
        Open(recent, "/b/B.csproj");
        fs.DeleteFile("/a/A.csproj");

        IReadOnlyList<RecentProject> list = recent.List();

        Assert.True(list.Single(entry => entry.Path == "/b/B.csproj").IsAvailable);
        Assert.False(list.Single(entry => entry.Path == "/a/A.csproj").IsAvailable);
    }

    [Fact]
    public void RemoveForgetsTheEntryAndNeverTouchesTheProjectFile()
    {
        RecentProjects recent = Create();
        Open(recent, "/a/A.csproj");

        recent.Remove("/a/A.csproj");

        Assert.Empty(recent.List());
        Assert.True(fs.FileExists("/a/A.csproj"));
    }

    [Fact]
    public void PathsAreComparedCaseSensitivelyOnLinuxAndMac()
    {
        RecentProjects recent = Create(StringComparison.Ordinal);
        recent.Record("/a/A.csproj", "A");
        recent.Record("/a/a.csproj", "a");

        Assert.Equal(2, recent.List().Count);
    }

    [Fact]
    public void PathsAreComparedCaseInsensitivelyOnWindows()
    {
        RecentProjects recent = Create(StringComparison.OrdinalIgnoreCase);
        recent.Record("C:/a/A.csproj", "A");
        time.Advance(TimeSpan.FromMinutes(1));
        recent.Record("c:/A/a.csproj", "A");
        recent.Pin("C:/A/A.CSPROJ");

        RecentProject entry = Assert.Single(recent.List());
        Assert.True(entry.Pinned);
        Assert.Equal("c:/A/a.csproj", entry.Path);
        recent.Remove("C:/a/a.csproj");
        Assert.Empty(recent.List());
    }

    [Fact]
    public void TheListSurvivesARestart()
    {
        RecentProjects first = Create();
        Open(first, "/a/A.csproj", "Alpha");
        first.Pin("/a/A.csproj");

        RecentProject entry = Assert.Single(Create().List());

        Assert.Equal(("/a/A.csproj", "Alpha", true), (entry.Path, entry.DisplayName, entry.Pinned));
    }
}
