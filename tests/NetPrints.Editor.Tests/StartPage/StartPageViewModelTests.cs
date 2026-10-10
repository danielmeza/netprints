using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Shell;
using NetPrints.Editor.StartPage;
using NetPrints.Editor.State;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Editor.Tests.Shell;
using NetPrints.Editor.Tests.State;

namespace NetPrints.Editor.Tests.StartPage;

/// <summary>The start page (FR-040, FR-041): its tiles, when it shows, the startup error and the recent list's commands.</summary>
public sealed class StartPageViewModelTests : IDisposable
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly TestEditor editor = TestEditor.Create(TestEditor.CreateReflectionHost);
    private readonly InMemoryEditorFileSystem fs = new();
    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 10, 1, 10, 0, 0, TimeSpan.Zero));
    private readonly FakeProjectActions actions = new();
    private readonly List<string> cleanup = [];
    private readonly List<ProjectRig> rigs = [];

    public void Dispose()
    {
        rigs.ForEach(rig => rig.Dispose());
        cleanup.ForEach(TestPaths.TryDelete);
    }

    private RecentProjects NewRecent() =>
        new(new JsonEditorStateStore(new EditorDataPaths("/state-root"), fs, NullLogger.Instance), fs, time);

    private void Record(RecentProjects recent, string path)
    {
        fs.WriteAllBytes(path, [1]);
        recent.Record(path, Path.GetFileNameWithoutExtension(path));
        time.Advance(TimeSpan.FromMinutes(1));
    }

    private static ShellViewModel NewShell(IContributionRegistry registry) =>
        new(registry, new StartPageServices(), new FakeTimeProvider(), new InlineDispatcher());

    private static ContributionRegistry BuiltInRegistry()
    {
        var registry = new ContributionRegistry(NullLogger<ContributionRegistry>.Instance);
        BuiltInContributions.Register(registry);
        return registry;
    }

    private static StartPageServices ServicesFor(IProjectActions actions, RecentProjects? recent) =>
        new StartPageServices().Add(actions).Add(recent ?? new RecentProjects(
            new JsonEditorStateStore(new EditorDataPaths("/none"), new InMemoryEditorFileSystem(), NullLogger.Instance), new InMemoryEditorFileSystem(), TimeProvider.System));

    [Fact]
    public void TheBuiltInTilesAreRegisteredInOrder()
    {
        Assert.Equal(
            ["netprints.tile.recent", "netprints.tile.open", "netprints.tile.new", "netprints.tile.samples", "netprints.tile.learn", "netprints.tile.whatsNew"],
            BuiltInRegistry().DashboardTiles.OrderBy(tile => tile.Order).Select(tile => tile.Id));
    }

    [Fact]
    public void ThePageBuildsItsTilesFromTheRegistryInOrder()
    {
        ContributionRegistry registry = BuiltInRegistry();
        registry.AddDashboardTile(new DashboardTileDescriptor("ext.tile.first", "First", -1, _ => "first"));
        using var page = new StartPageViewModel(NewShell(registry), ServicesFor(actions, null));

        Assert.Equal(
            [typeof(string), typeof(RecentProjectsTileViewModel), typeof(OpenProjectTileViewModel), typeof(NewProjectTileViewModel), typeof(SamplesTileViewModel), typeof(LearnTileViewModel), typeof(WhatsNewTileViewModel)],
            page.Tiles.Select(tile => tile.GetType()));
        Assert.Equal(DocumentId.StartPage, page.Id);
    }

    [Fact]
    public void ThePageSplitsItsTilesByRoleForTheLayout()
    {
        ContributionRegistry registry = BuiltInRegistry();
        registry.AddDashboardTile(new DashboardTileDescriptor("ext.tile.extra", "Extra", 9, _ => "extra"));
        using var page = new StartPageViewModel(NewShell(registry), ServicesFor(actions, null));

        Assert.IsType<RecentProjectsTileViewModel>(page.Recent);
        Assert.Equal([typeof(NewProjectTileViewModel), typeof(OpenProjectTileViewModel)], page.GetStarted.Select(card => card.GetType()).Order(Comparer<Type>.Create((a, b) => string.CompareOrdinal(a.Name, b.Name))));
        Assert.IsType<SamplesTileViewModel>(page.Samples);
        Assert.IsType<LearnTileViewModel>(page.Learn);
        Assert.IsType<WhatsNewTileViewModel>(page.WhatsNew);
        Assert.Equal(["extra"], page.Others);
        Assert.False(string.IsNullOrWhiteSpace(page.Version));
    }

    private sealed class RecordingLauncher : IUrlLauncher
    {
        public List<string> Opened { get; } = [];

        public void Open(string url) => Opened.Add(url);
    }

    [Fact]
    public void TheLearnCardOpensTheGuideTheDocumentationAndTheReleaseNotesThroughTheLauncher()
    {
        var launcher = new RecordingLauncher();
        var tile = new LearnTileViewModel(actions, launcher);

        tile.OpenGuideCommand.Execute(null);
        tile.OpenDocumentationCommand.Execute(null);
        tile.OpenReleaseNotesCommand.Execute(null);

        Assert.Equal(
            ["https://danielmeza.github.io/netprints/guide/projects", "https://danielmeza.github.io/netprints/", "https://github.com/danielmeza/netprints/releases"],
            launcher.Opened);
        Assert.Equal([LearnLinks.Guide, LearnLinks.Documentation, LearnLinks.ReleaseNotes], launcher.Opened);
        Assert.Empty(actions.Calls);
    }

    [Fact]
    public async Task TheLearnCardShowsTheKeyboardShortcutsSheetWithoutTheUnloadPromptOrABrowser()
    {
        var launcher = new RecordingLauncher();
        var tile = new LearnTileViewModel(actions, launcher);

        await tile.ShowKeyboardShortcutsCommand.ExecuteAsync(null);

        Assert.Equal(["ShowKeyboardShortcuts"], actions.Calls);
        Assert.Empty(launcher.Opened);
    }

    [Fact]
    public void TheLearnAddressesAreAbsoluteHttpsAddresses()
    {
        Assert.All([LearnLinks.Guide, LearnLinks.Documentation, LearnLinks.ReleaseNotes],
            address => Assert.True(Uri.TryCreate(address, UriKind.Absolute, out Uri? uri) && uri.Scheme == Uri.UriSchemeHttps, address));
    }

    [Fact]
    public void WhatsNewStartsExpandedUntilThisVersionsNotesWereShownOnce()
    {
        var store = new JsonEditorStateStore(new EditorDataPaths("/state-root"), fs, NullLogger.Instance);
        StartPageServices services = ServicesFor(actions, null).Add<IEditorStateStore>(store);

        using (var first = new StartPageViewModel(NewShell(BuiltInRegistry()), services))
        {
            Assert.True(first.IsWhatsNewExpanded);
        }

        Assert.Equal(StartPageViewModel.ProductVersion, store.LoadStart()?.WhatsNewSeenVersion);
        using var second = new StartPageViewModel(NewShell(BuiltInRegistry()), services);
        Assert.False(second.IsWhatsNewExpanded);
    }

    [Fact]
    public void TheToggleCommandOpensAndClosesWhatsNew()
    {
        using var page = new StartPageViewModel(NewShell(BuiltInRegistry()), ServicesFor(actions, null));
        bool before = page.IsWhatsNewExpanded;

        page.ToggleWhatsNewCommand.Execute(null);
        Assert.Equal(!before, page.IsWhatsNewExpanded);

        page.ToggleWhatsNewCommand.Execute(null);
        Assert.Equal(before, page.IsWhatsNewExpanded);
    }

    [Fact]
    public void AnotherVersionsNotesAreNewAgain()
    {
        var store = new JsonEditorStateStore(new EditorDataPaths("/state-root"), fs, NullLogger.Instance);
        store.SaveStart(new StartState(StateFile.CurrentVersion, "0.0.1-old"));
        StartPageServices services = ServicesFor(actions, null).Add<IEditorStateStore>(store);

        using var page = new StartPageViewModel(NewShell(BuiltInRegistry()), services);

        Assert.True(page.IsWhatsNewExpanded);
    }

    [Fact]
    public void ThePageShowsTheShellsStartError()
    {
        ShellViewModel shell = NewShell(BuiltInRegistry());
        shell.StartPageError = "'/x/y.csproj' does not exist";
        using var page = new StartPageViewModel(shell, ServicesFor(actions, null));
        Assert.Equal("'/x/y.csproj' does not exist", page.Error);

        shell.StartPageError = null;
        Assert.Null(page.Error);
    }

    [Fact]
    public void TheStartPageOpensWithoutAProjectAndClosesWithOne()
    {
        var shell = new FakeShell();
        var controller = new StartPageController(shell);

        controller.Sync(projectOpen: false);
        Assert.Equal([DocumentId.StartPage], shell.OpenDocuments);

        controller.Sync(projectOpen: true);
        Assert.Empty(shell.OpenDocuments);
    }

    private ProjectRig NewRig()
    {
        var rig = new ProjectRig(editor.Context);
        rigs.Add(rig);
        return rig;
    }

    [Fact]
    public async Task AStartupPathThatDoesNotExistShowsTheStartPageWithAnErrorNamingIt()
    {
        ProjectRig rig = NewRig();
        string missing = Path.Combine(TestPaths.CreateTempDirectory(), "Missing.csproj");
        cleanup.Add(missing);

        await rig.Actions.Loader.OpenStartupProjectAsync([missing]);

        Assert.Null(rig.Session);
        Assert.Contains(missing, rig.Shell.StartPageError, StringComparison.Ordinal);
        Assert.Empty(editor.Dialogs.Errors);
    }

    [Fact]
    public async Task AStartupPathThatIsNotAProjectShowsTheStartPageWithAnErrorNamingIt()
    {
        ProjectRig rig = NewRig();
        string directory = TestPaths.CreateTempDirectory();
        cleanup.Add(directory);
        string notes = Path.Combine(directory, "notes.txt");
        File.WriteAllText(notes, "text");

        await rig.Actions.Loader.OpenStartupProjectAsync([notes]);

        Assert.Null(rig.Session);
        Assert.Contains(notes, rig.Shell.StartPageError, StringComparison.Ordinal);

        await rig.Actions.Loader.OpenStartupProjectAsync([directory]);
        Assert.Contains(directory, rig.Shell.StartPageError, StringComparison.Ordinal);
        Assert.Empty(editor.Dialogs.Errors);
    }

    [Fact]
    public async Task OpeningAProjectClearsTheStartError()
    {
        ProjectRig rig = NewRig();
        rig.Shell.StartPageError = "old";
        string path = TestPaths.CopyHelloWorldSample();
        cleanup.Add(path);

        await rig.Actions.Loader.OpenStartupProjectAsync([path]);

        Assert.NotNull(rig.Session);
        Assert.Null(rig.Shell.StartPageError);
    }

    [Fact]
    public void TheRecentTileListsPinnedFirstAndSearchesByNameOrPath()
    {
        RecentProjects recent = NewRecent();
        Record(recent, "/a/Alpha.csproj");
        Record(recent, "/b/Beta.csproj");
        Record(recent, "/c/Gamma.csproj");
        recent.Pin("/a/Alpha.csproj");
        var tile = new RecentProjectsTileViewModel(recent, actions);

        Assert.Equal(["/a/Alpha.csproj", "/c/Gamma.csproj", "/b/Beta.csproj"], tile.Items.Select(item => item.Path));

        tile.SearchText = "beta";
        Assert.Equal(["/b/Beta.csproj"], tile.Items.Select(item => item.Path));

        tile.SearchText = "/c/";
        Assert.Equal(["/c/Gamma.csproj"], tile.Items.Select(item => item.Path));

        tile.SearchText = "nothing";
        Assert.True(tile.IsEmpty);
    }

    [Fact]
    public void PinUnpinAndRemoveUpdateTheListAndTheRecentStore()
    {
        RecentProjects recent = NewRecent();
        Record(recent, "/a/Alpha.csproj");
        Record(recent, "/b/Beta.csproj");
        var tile = new RecentProjectsTileViewModel(recent, actions);
        RecentProjectItemViewModel alpha = tile.Items.Single(item => item.DisplayName == "Alpha");

        tile.PinCommand.Execute(alpha);
        Assert.Equal(["/a/Alpha.csproj", "/b/Beta.csproj"], tile.Items.Select(item => item.Path));
        Assert.True(tile.Items[0].Pinned);
        Assert.True(recent.List()[0].Pinned);

        tile.UnpinCommand.Execute(tile.Items[0]);
        Assert.Equal(["/b/Beta.csproj", "/a/Alpha.csproj"], tile.Items.Select(item => item.Path));
        Assert.False(tile.Items[1].Pinned);

        tile.RemoveCommand.Execute(tile.Items[0]);
        Assert.Equal(["/a/Alpha.csproj"], tile.Items.Select(item => item.Path));
        Assert.Equal(["/a/Alpha.csproj"], recent.List().Select(entry => entry.Path));
    }

    [Fact]
    public async Task OpeningAnEntryOpensItsPathAndAnUnavailableOneCannotOpenButCanBeRemoved()
    {
        RecentProjects recent = NewRecent();
        Record(recent, "/a/Alpha.csproj");
        Record(recent, "/b/Beta.csproj");
        fs.DeleteFile("/a/Alpha.csproj");
        var tile = new RecentProjectsTileViewModel(recent, actions);
        await tile.AvailabilityChecked;
        RecentProjectRow(tile, "Beta", out RecentProjectItemViewModel beta);
        RecentProjectRow(tile, "Alpha", out RecentProjectItemViewModel alpha);

        await tile.OpenCommand.ExecuteAsync(beta);
        Assert.Equal(["ConfirmUnload", "OpenProject:/b/Beta.csproj"], actions.Calls);

        Assert.False(alpha.IsAvailable);
        Assert.False(tile.OpenCommand.CanExecute(alpha));
        Assert.True(tile.RemoveCommand.CanExecute(alpha));
    }

    private static void RecentProjectRow(RecentProjectsTileViewModel tile, string name, out RecentProjectItemViewModel item) =>
        item = tile.Items.Single(row => row.DisplayName == name);

    [Fact]
    public async Task TheOpenAndNewTilesCallTheProjectActions()
    {
        await new OpenProjectTileViewModel(actions).OpenCommand.ExecuteAsync(null);
        await new NewProjectTileViewModel(actions).NewCommand.ExecuteAsync(null);

        Assert.Equal(["ConfirmUnload", "OpenProject:", "ConfirmUnload", "NewProject"], actions.Calls);
    }

    [Fact]
    public async Task DecliningTheUnloadPromptKeepsTheOpenProjectFromEveryTile()
    {
        actions.AllowUnload = false;
        RecentProjects recent = NewRecent();
        Record(recent, "/a/Alpha.csproj");
        var tile = new RecentProjectsTileViewModel(recent, actions);

        await new OpenProjectTileViewModel(actions).OpenCommand.ExecuteAsync(null);
        await new NewProjectTileViewModel(actions).NewCommand.ExecuteAsync(null);
        await tile.OpenCommand.ExecuteAsync(tile.Items[0]);

        Assert.Equal(["ConfirmUnload", "ConfirmUnload", "ConfirmUnload"], actions.Calls);
    }
}
