using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Shell;
using NetPrints.Editor.StartPage;
using NetPrints.Editor.State;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Editor.Tests.State;

namespace NetPrints.Editor.Tests.StartPage;

/// <summary>The startup setting (FR-048): reopen the last project or show the start page, a project argument always wins, and a failed reopen shows the start page with the error.</summary>
public sealed class StartupBehaviorTests : IDisposable
{
    private readonly TestEditor editor = TestEditor.Create(TestEditor.CreateReflectionHost);
    private readonly InMemoryEditorFileSystem fileSystem = new();
    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 10, 1, 10, 0, 0, TimeSpan.Zero));
    private readonly List<string> cleanup = [];
    private readonly List<ProjectRig> rigs = [];
    private readonly JsonEditorStateStore store;
    private readonly RecentProjects recent;

    public StartupBehaviorTests()
    {
        store = new JsonEditorStateStore(new EditorDataPaths("/state-root"), fileSystem, NullLogger.Instance);
        recent = new RecentProjects(store, fileSystem, time);
    }

    public void Dispose()
    {
        rigs.ForEach(rig => rig.Dispose());
        cleanup.ForEach(TestPaths.TryDelete);
    }

    private ProjectRig NewRig(StartupBehavior behavior, IEditorStateStore? stateStore = null)
    {
        store.Update(state => state with { StartupBehavior = behavior });
        var rig = new ProjectRig(editor.Context with { StateStore = stateStore ?? store, Recent = recent });
        rigs.Add(rig);
        return rig;
    }

    private string Sample()
    {
        string path = TestPaths.CopyHelloWorldSample();
        cleanup.Add(path);
        fileSystem.WriteAllBytes(path, [1]);
        return path;
    }

    private void Record(string path)
    {
        recent.Record(path, Path.GetFileNameWithoutExtension(path));
        time.Advance(TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task ReopenTheLastProjectOpensTheMostRecentOneEvenWhenAnotherIsPinned()
    {
        string older = Sample();
        string last = Sample();
        Record(older);
        Record(last);
        recent.Pin(older);
        ProjectRig rig = NewRig(StartupBehavior.ReopenLastProject);

        await rig.Actions.Loader.OpenStartupProjectAsync([]);

        Assert.Equal(last, rig.Project?.Path);
        Assert.Null(rig.Shell.StartPageError);
    }

    [Fact]
    public async Task ShowTheStartPageOpensNothingEvenWithRecentProjects()
    {
        Record(Sample());
        ProjectRig rig = NewRig(StartupBehavior.ShowStartPage);

        await rig.Actions.Loader.OpenStartupProjectAsync([]);

        Assert.Null(rig.Session);
        Assert.Null(rig.Shell.StartPageError);
    }

    [Fact]
    public async Task ASettingThatWasNeverSavedShowsTheStartPage()
    {
        Record(Sample());
        var rig = new ProjectRig(editor.Context with { StateStore = store, Recent = recent });
        rigs.Add(rig);

        await rig.Actions.Loader.OpenStartupProjectAsync([]);

        Assert.Null(rig.Session);
    }

    [Fact]
    public async Task AProjectArgumentWinsOverReopeningTheLastProject()
    {
        string last = Sample();
        string argument = Sample();
        Record(last);
        ProjectRig rig = NewRig(StartupBehavior.ReopenLastProject);

        await rig.Actions.Loader.OpenStartupProjectAsync([argument]);

        Assert.Equal(argument, rig.Project?.Path);
    }

    [Fact]
    public async Task ReopeningWithNoRecentProjectShowsTheStartPageWithoutAnError()
    {
        ProjectRig rig = NewRig(StartupBehavior.ReopenLastProject);

        await rig.Actions.Loader.OpenStartupProjectAsync([]);

        Assert.Null(rig.Session);
        Assert.Null(rig.Shell.StartPageError);
    }

    [Fact]
    public async Task ALastProjectThatIsGoneShowsTheStartPageWithTheError()
    {
        string gone = Path.Combine(TestPaths.CreateTempDirectory(), "Gone.csproj");
        cleanup.Add(gone);
        recent.Record(gone, "Gone");
        ProjectRig rig = NewRig(StartupBehavior.ReopenLastProject);

        await rig.Actions.Loader.OpenStartupProjectAsync([]);

        Assert.Null(rig.Session);
        Assert.Contains(gone, rig.Shell.StartPageError, StringComparison.Ordinal);
        Assert.Empty(editor.Dialogs.Errors);
    }

    [Fact]
    public async Task ALastProjectThatFailsToLoadShowsTheStartPageWithTheError()
    {
        string broken = Path.Combine(TestPaths.CreateTempDirectory(), "Broken.csproj");
        cleanup.Add(broken);
        fileSystem.WriteAllBytes(broken, [1]);
        recent.Record(broken, "Broken");
        ProjectRig rig = NewRig(StartupBehavior.ReopenLastProject);

        await rig.Actions.Loader.OpenStartupProjectAsync([]);

        Assert.Null(rig.Session);
        Assert.Contains(broken, rig.Shell.StartPageError, StringComparison.Ordinal);
    }

    private sealed class MarkRecordingStore(IEditorStateStore inner) : IEditorStateStore
    {
        public List<bool> Marks { get; } = [];

        public WindowState? LoadWindow() => inner.LoadWindow();

        public void SaveWindow(WindowState state) => inner.SaveWindow(state);

        public LayoutState? LoadLayout() => inner.LoadLayout();

        public void SaveLayout(LayoutState state, bool userChanged = false) => inner.SaveLayout(state, userChanged);

        public RecentState LoadRecent() => inner.LoadRecent();

        public void SaveRecent(RecentState state) => inner.SaveRecent(state);

        public SessionState? LoadSession(string projectPath) => inner.LoadSession(projectPath);

        public void SaveSession(SessionState state) => inner.SaveSession(state);

        public StartState? LoadStart() => inner.LoadStart();

        public void SaveStart(StartState state, bool userChanged = false)
        {
            Marks.Add(state.ReopenInProgress);
            inner.SaveStart(state, userChanged);
        }
    }

    [Fact]
    public async Task AReopenThatNeverFinishedShowsTheStartPageWithAMessageAndLoadsNothing()
    {
        Record(Sample());
        ProjectRig rig = NewRig(StartupBehavior.ReopenLastProject);
        store.Update(state => state with { ReopenInProgress = true });

        await rig.Actions.Loader.OpenStartupProjectAsync([]);

        Assert.Null(rig.Session);
        Assert.Contains("did not open last time", rig.Shell.StartPageError, StringComparison.Ordinal);
        Assert.False(store.LoadStart()?.ReopenInProgress);
        Assert.Equal(StartupBehavior.ReopenLastProject, store.LoadStart()?.StartupBehavior);
    }

    [Fact]
    public async Task TheReopenMarkIsSetBeforeTheLoadAndClearedWhenItEnds()
    {
        string last = Sample();
        Record(last);
        var marking = new MarkRecordingStore(store);
        ProjectRig rig = NewRig(StartupBehavior.ReopenLastProject, marking);
        marking.Marks.Clear();

        await rig.Actions.Loader.OpenStartupProjectAsync([]);

        Assert.Equal(last, rig.Project?.Path);
        Assert.Equal([true, false], marking.Marks);
        Assert.False(store.LoadStart()?.ReopenInProgress);
    }

    [Fact]
    public async Task TheReopenMarkIsClearedWhenTheLoadFails()
    {
        string broken = Path.Combine(TestPaths.CreateTempDirectory(), "Broken.csproj");
        cleanup.Add(broken);
        fileSystem.WriteAllBytes(broken, [1]);
        recent.Record(broken, "Broken");
        ProjectRig rig = NewRig(StartupBehavior.ReopenLastProject);

        await rig.Actions.Loader.OpenStartupProjectAsync([]);

        Assert.False(store.LoadStart()?.ReopenInProgress);
        Assert.Contains("could not be opened", rig.Shell.StartPageError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AProjectArgumentIgnoresTheReopenMark()
    {
        string argument = Sample();
        ProjectRig rig = NewRig(StartupBehavior.ReopenLastProject);
        store.Update(state => state with { ReopenInProgress = true });

        await rig.Actions.Loader.OpenStartupProjectAsync([argument]);

        Assert.Equal(argument, rig.Project?.Path);
    }

    [Fact]
    public void TheStartupBehaviorIsWrittenAsTheNameOfTheValue()
    {
        store.SaveStart(new StartState(StateFile.CurrentVersion, null, null, StartupBehavior.ReopenLastProject));

        Assert.Contains("\"startupBehavior\": \"ReopenLastProject\"", System.Text.Encoding.UTF8.GetString(fileSystem.ReadAllBytes(Path.Combine("/state-root", "state", "start.json"))), StringComparison.Ordinal);
        Assert.Equal(StartupBehavior.ReopenLastProject, store.LoadStart()?.StartupBehavior);
    }

    private StartPageViewModel Page(IEditorStateStore? withStore)
    {
        var registry = new ContributionRegistry(NullLogger<ContributionRegistry>.Instance);
        BuiltInContributions.Register(registry);
        var services = new StartPageServices().Add<IProjectActions>(new Shell.FakeProjectActions()).Add(recent);
        if (withStore is not null)
        {
            services.Add(withStore);
        }

        return new StartPageViewModel(new ShellViewModel(registry, services, new FakeTimeProvider(), new InlineDispatcher()), services);
    }

    [Fact]
    public void TheCheckboxShowsTheSavedSettingAndSavesAChangeKeepingTheRest()
    {
        store.SaveStart(new StartState(StateFile.CurrentVersion, "0.0.1", "/work", StartupBehavior.ReopenLastProject));
        using StartPageViewModel page = Page(store);

        Assert.True(page.ReopenLastProject);
        page.ReopenLastProject = false;

        StartState? saved = store.LoadStart();
        Assert.Equal(StartupBehavior.ShowStartPage, saved?.StartupBehavior);
        Assert.Equal("/work", saved?.NewProjectLocation);
        Assert.NotNull(saved?.WhatsNewSeenVersion);
        page.ReopenLastProject = true;
        Assert.Equal(StartupBehavior.ReopenLastProject, store.LoadStart()?.StartupBehavior);
    }

    [Fact]
    public void TheCheckboxIsOffAndWritesNothingByItselfWithNoSavedSetting()
    {
        using StartPageViewModel page = Page(store);

        Assert.False(page.ReopenLastProject);
        Assert.Equal(StartupBehavior.ShowStartPage, store.LoadStart()?.StartupBehavior ?? StartupBehavior.ShowStartPage);
    }
}
