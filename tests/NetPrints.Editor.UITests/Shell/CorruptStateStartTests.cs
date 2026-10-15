using Avalonia.Headless.XUnit;
using Microsoft.Extensions.Logging;
using NetPrints.Editor.Shell;
using NetPrints.Editor.StartPage;
using NetPrints.Editor.State;
using NetPrints.Editor.UITests.Hosting;

namespace NetPrints.Editor.UITests.Shell;

/// <summary>The composed editor starts on the start page, with the defaults and a logged warning, over state files it cannot use (R1, R6): the recent list logs 1200 for an entry it cannot use.</summary>
public class CorruptStateStartTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "netprints-corrupt-" + Guid.NewGuid().ToString("N"));

    public void Dispose() => Directory.Delete(root, recursive: true);

    private ShellApp StartWith(string file, string content, RecordingLoggerFactory logs)
    {
        var paths = new EditorDataPaths(root);
        Directory.CreateDirectory(paths.StateDirectory);
        File.WriteAllText(Path.Combine(paths.StateDirectory, file), content);
        var store = new JsonEditorStateStore(paths, new RealEditorFileSystem(), logs.CreateLogger(nameof(JsonEditorStateStore)));
        return ShellApp.Start(store, logs);
    }

    [Fact]
    public async Task ARecentFileWithANullEntryGivesAnEmptyRecentTileThatSearchesWithAWarning()
    {
        var logs = new RecordingLoggerFactory();
        var paths = new EditorDataPaths(root);
        Directory.CreateDirectory(paths.StateDirectory);
        File.WriteAllText(Path.Combine(paths.StateDirectory, "recent.json"), """{"schemaVersion":1,"entries":[null,{"path":"/p/A.csproj","displayName":"A","lastOpenedUtc":"2026-10-01T09:00:00Z","pinned":false}]}""");
        var store = new JsonEditorStateStore(paths, new RealEditorFileSystem(), logs.CreateLogger(nameof(JsonEditorStateStore)));

        var tile = new RecentProjectsTileViewModel(new RecentProjects(store, new RealEditorFileSystem(), TimeProvider.System), new NoProjectActions());
        tile.SearchText = "hello";
        await tile.AvailabilityChecked;

        Assert.Contains(1200, logs.Ids);
        Assert.True(tile.IsEmpty);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task ALayoutThatIsValidJsonButUnusableFallsBackToTheDefaultLayoutWithAWarning()
    {
        var logs = new RecordingLoggerFactory();

        await using ShellApp app = StartWith("layout.json", """{"schemaVersion":1,"engine":"dock","dockLayout":{"unusable":true}}""", logs);

        Assert.Contains(1210, logs.Ids);
        Assert.NotEmpty(app.Shell.Panels);
        Assert.Contains(DocumentId.StartPage, app.Api.OpenDocuments);
    }
}
