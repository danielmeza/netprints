using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.StartPage;
using NetPrints.Editor.State;
using NetPrints.Editor.Tests.Shell;
using NetPrints.Editor.Tests.State;

namespace NetPrints.Editor.Tests.StartPage;

/// <summary>The recent list as rows: date groups, relative dates, the unavailable row, the context menu and the keyboard commands (FR-047).</summary>
public sealed class RecentProjectsTileViewModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

    private readonly InMemoryEditorFileSystem fs = new();
    private readonly FakeTimeProvider time = new(Now);
    private readonly SettableClock recordClock = new();
    private readonly FakeProjectActions actions = new();
    private readonly RecordingClipboard clipboard = new();
    private readonly RecordingFolders folders = new();

    private sealed class SettableClock : TimeProvider
    {
        public DateTimeOffset Value { get; set; }

        public override DateTimeOffset GetUtcNow() => Value;
    }

    private sealed class RecordingClipboard : IClipboardService
    {
        public List<string> Texts { get; } = [];

        public Task SetTextAsync(string text)
        {
            Texts.Add(text);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingFolders : IFolderLauncher
    {
        public List<string> Revealed { get; } = [];

        public void Reveal(string path) => Revealed.Add(path);
    }

    private RecentProjects NewRecent() =>
        new(new JsonEditorStateStore(new EditorDataPaths("/state-root"), fs, NullLogger.Instance), fs, recordClock);

    private void Record(RecentProjects recent, string path, TimeSpan age, bool exists = true)
    {
        if (exists)
        {
            fs.WriteAllBytes(path, [1]);
        }

        recordClock.Value = Now - age;
        recent.Record(path, Path.GetFileNameWithoutExtension(path));
    }

    private RecentProjectsTileViewModel NewTile(RecentProjects recent) => new(recent, actions, time, clipboard, folders);

    private RecentProjectsTileViewModel FiveRows()
    {
        RecentProjects recent = NewRecent();
        Record(recent, "/p/Old.csproj", TimeSpan.FromDays(60));
        Record(recent, "/p/Month.csproj", TimeSpan.FromDays(15));
        Record(recent, "/p/Week.csproj", TimeSpan.FromDays(3));
        Record(recent, "/p/Today.csproj", TimeSpan.FromHours(2));
        Record(recent, "/p/Pinned.csproj", TimeSpan.FromDays(100));
        recent.Pin("/p/Pinned.csproj");
        return NewTile(recent);
    }

    [Theory]
    [InlineData(0, "Just now")]
    [InlineData(30, "Just now")]
    [InlineData(60, "1 minute ago")]
    [InlineData(5 * 60, "5 minutes ago")]
    [InlineData(60 * 60, "1 hour ago")]
    [InlineData(2 * 60 * 60, "2 hours ago")]
    [InlineData(30 * 60 * 60, "Yesterday")]
    [InlineData(3 * 24 * 60 * 60, "3 days ago")]
    [InlineData(20 * 24 * 60 * 60, "16 Sep 2026")]
    public void TheRelativeDateIsWrittenInTheUnitsAPersonUses(int secondsAgo, string expected) =>
        Assert.Equal(expected, RecentTime.Describe(Now.AddSeconds(-secondsAgo), Now, TimeZoneInfo.Utc, CultureInfo.InvariantCulture));

    [Theory]
    [InlineData(2, false, "Today")]
    [InlineData(30, false, "This week")]
    [InlineData(3 * 24, false, "This week")]
    [InlineData(15 * 24, false, "This month")]
    [InlineData(60 * 24, false, "Older")]
    [InlineData(60 * 24, true, "Pinned")]
    public void TheGroupFollowsThePinAndTheAgeInDays(int hoursAgo, bool pinned, string expected)
    {
        string group = RecentTime.GroupOf(pinned, Now.AddHours(-hoursAgo), Now, TimeZoneInfo.Utc);

        Assert.Equal(expected, group);
    }

    [Fact]
    public void TheRowsAreGroupedAndOnlyTheFirstRowOfAGroupShowsItsHeader()
    {
        RecentProjectsTileViewModel tile = FiveRows();

        Assert.Equal(["Pinned", "Today", "This week", "This month", "Older"], tile.Items.Select(item => item.GroupTitle));
        Assert.All(tile.Items, item => Assert.True(item.ShowGroupHeader));
        Assert.Equal(["2 hours ago", "3 days ago"], tile.Items.Skip(1).Take(2).Select(item => item.RelativeDate));
    }

    [Fact]
    public void ASecondRowOfAGroupHasNoHeader()
    {
        RecentProjects recent = NewRecent();
        Record(recent, "/p/A.csproj", TimeSpan.FromHours(1));
        Record(recent, "/p/B.csproj", TimeSpan.FromHours(3));

        RecentProjectsTileViewModel tile = NewTile(recent);

        Assert.Equal([true, false], tile.Items.Select(item => item.ShowGroupHeader));
    }

    [Fact]
    public async Task TheRowsShowBeforeTheAvailabilityCheckReturnsAndThenUpdate()
    {
        RecentProjects recent = NewRecent();
        Record(recent, "/p/Slow.csproj", TimeSpan.FromHours(1), exists: false);
        using var release = new ManualResetEventSlim();
        int callerThread = Environment.CurrentManagedThreadId;
        List<int> checkingThreads = [];
        fs.BeforeFileExists = path =>
        {
            if (path == "/p/Slow.csproj")
            {
                lock (checkingThreads)
                {
                    checkingThreads.Add(Environment.CurrentManagedThreadId);
                }

                release.Wait(TimeSpan.FromSeconds(10));
            }
        };

        RecentProjectsTileViewModel tile = NewTile(recent);

        RecentProjectItemViewModel row = Assert.Single(tile.Items);
        Assert.False(tile.AvailabilityChecked.IsCompleted);
        Assert.True(row.IsAvailable);
        release.Set();
        await tile.AvailabilityChecked;
        Assert.False(row.IsAvailable);
        Assert.Equal("Not found", row.StatusText);
        Assert.DoesNotContain(callerThread, checkingThreads);
    }

    [Fact]
    public async Task SearchingAndPinningDoNotCheckTheFilesAgain()
    {
        RecentProjects recent = NewRecent();
        Record(recent, "/p/A.csproj", TimeSpan.FromHours(1));
        RecentProjectsTileViewModel tile = NewTile(recent);
        await tile.AvailabilityChecked;
        List<string> checks = [];
        fs.BeforeFileExists = checks.Add;

        tile.SearchText = "A";
        tile.PinCommand.Execute(Assert.Single(tile.Items));

        Assert.DoesNotContain("/p/A.csproj", checks);
        Assert.True(tile.AvailabilityChecked.IsCompleted);
    }

    [Fact]
    public async Task AnUnavailableRowSaysNotFoundCannotOpenAndCanBeRemoved()
    {
        RecentProjects recent = NewRecent();
        Record(recent, "/p/Gone.csproj", TimeSpan.FromHours(1), exists: false);
        RecentProjectsTileViewModel tile = NewTile(recent);
        await tile.AvailabilityChecked;
        RecentProjectItemViewModel row = Assert.Single(tile.Items);

        Assert.False(row.IsAvailable);
        Assert.Equal("Not found", row.StatusText);
        Assert.False(tile.OpenCommand.CanExecute(row));
        tile.RemoveCommand.Execute(row);
        Assert.True(tile.IsEmpty);
    }

    [Fact]
    public void AnAvailableRowHasNoStatusText()
    {
        RecentProjectsTileViewModel tile = FiveRows();

        Assert.All(tile.Items, item => Assert.Null(item.StatusText));
    }

    [Fact]
    public async Task CopyPathPutsTheProjectPathOnTheClipboard()
    {
        RecentProjectsTileViewModel tile = FiveRows();

        await tile.CopyPathCommand.ExecuteAsync(tile.Items[1]);

        Assert.Equal(["/p/Today.csproj"], clipboard.Texts);
    }

    [Fact]
    public void OpenContainingFolderRevealsTheProjectFolder()
    {
        RecentProjectsTileViewModel tile = FiveRows();

        tile.OpenContainingFolderCommand.Execute(tile.Items[1]);

        Assert.Equal(["/p"], folders.Revealed);
    }

    [Fact]
    public void TogglePinPinsAnUnpinnedRowAndUnpinsAPinnedOne()
    {
        RecentProjectsTileViewModel tile = FiveRows();

        tile.TogglePinCommand.Execute(tile.Items[1]);
        Assert.Equal(["Pinned", "Pinned"], tile.Items.Take(2).Select(item => item.GroupTitle));

        tile.TogglePinCommand.Execute(tile.Items[0]);
        Assert.Equal(1, tile.Items.Count(item => item.Pinned));
    }

    [Fact]
    public async Task EnterOpensTheSelectedRowDeleteRemovesItAndCtrlPTogglesItsPin()
    {
        RecentProjectsTileViewModel tile = FiveRows();
        tile.SelectedItem = tile.Items[2];

        await tile.OpenSelectedCommand.ExecuteAsync(null);
        Assert.Equal(["OpenProject:/p/Week.csproj"], actions.Calls.Where(call => call.StartsWith("OpenProject", StringComparison.Ordinal)));

        tile.TogglePinSelectedCommand.Execute(null);
        Assert.True(tile.Items.Single(item => item.Path == "/p/Week.csproj").Pinned);
        Assert.Equal("/p/Week.csproj", tile.SelectedItem?.Path);

        tile.RemoveSelectedCommand.Execute(null);
        Assert.DoesNotContain(tile.Items, item => item.Path == "/p/Week.csproj");
        Assert.NotNull(tile.SelectedItem);
    }

    [Fact]
    public void WithNothingSelectedTheKeyboardCommandsCannotRun()
    {
        RecentProjectsTileViewModel tile = FiveRows();
        tile.SelectedItem = null;

        Assert.False(tile.OpenSelectedCommand.CanExecute(null));
        Assert.False(tile.RemoveSelectedCommand.CanExecute(null));
        Assert.False(tile.TogglePinSelectedCommand.CanExecute(null));
    }

    [Fact]
    public void FocusSearchMovesToTheFirstRowWhenThereIsOne()
    {
        RecentProjectsTileViewModel tile = FiveRows();

        tile.SelectFirstCommand.Execute(null);

        Assert.Same(tile.Items[0], tile.SelectedItem);
    }

    [Fact]
    public void ALongPathIsTrimmedInTheMiddleAndAShortOneIsKept()
    {
        string longPath = "/data/user/projects/" + new string('d', 80) + "/Game.csproj";

        string shown = RecentProjectItemViewModel.MiddleTrim(longPath);

        Assert.True(shown.Length <= RecentProjectItemViewModel.MaxPathLength);
        Assert.StartsWith("/data/user", shown, StringComparison.Ordinal);
        Assert.EndsWith("/Game.csproj", shown, StringComparison.Ordinal);
        Assert.Contains('…', shown);
        Assert.Equal("/p/App.csproj", RecentProjectItemViewModel.MiddleTrim("/p/App.csproj"));
    }
}
