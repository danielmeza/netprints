using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Extensibility.Editor;

namespace NetPrints.Editor.Tests.Shell;

/// <summary>The status bar's message expiry runs on the injected clock; the build state has its own text.</summary>
public class StatusBarViewModelTests
{
    private readonly FakeTimeProvider time = new();

    [Fact]
    public void AMessageWithAnExpiryDisappearsWhenItIsOver()
    {
        var bar = new StatusBarViewModel(time, new InlineDispatcher());

        bar.Show("Undid: Add node", TimeSpan.FromSeconds(4));
        time.Advance(TimeSpan.FromSeconds(3));
        Assert.Equal("Undid: Add node", bar.Message);

        time.Advance(TimeSpan.FromSeconds(1));
        Assert.Null(bar.Message);
    }

    [Fact]
    public void AMessageWithoutAnExpiryStaysUntilReplaced()
    {
        var bar = new StatusBarViewModel(time, new InlineDispatcher());

        bar.Show("Build succeeded");
        time.Advance(TimeSpan.FromHours(1));

        Assert.Equal("Build succeeded", bar.Message);
    }

    [Fact]
    public void ANewMessageRestartsTheExpiryAndTheOldTimerNeverClearsIt()
    {
        var bar = new StatusBarViewModel(time, new InlineDispatcher());
        bar.Show("first", TimeSpan.FromSeconds(4));
        time.Advance(TimeSpan.FromSeconds(3));

        bar.Show("second", TimeSpan.FromSeconds(4));
        time.Advance(TimeSpan.FromSeconds(3));
        Assert.Equal("second", bar.Message);

        time.Advance(TimeSpan.FromSeconds(1));
        Assert.Null(bar.Message);
    }

    [Fact]
    public void AMessageWithoutAnExpiryCancelsThePreviousExpiry()
    {
        var bar = new StatusBarViewModel(time, new InlineDispatcher());
        bar.Show("first", TimeSpan.FromSeconds(4));

        bar.Show("kept");
        time.Advance(TimeSpan.FromSeconds(10));

        Assert.Equal("kept", bar.Message);
    }

    [Fact]
    public void TheBuildStateHasTextAndRaisesChanges()
    {
        var bar = new StatusBarViewModel(time, new InlineDispatcher());
        var changed = new List<string?>();
        bar.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        Assert.Equal("", bar.BuildStateText);

        bar.SetBuildState(BuildState.Building);
        Assert.Equal("Building…", bar.BuildStateText);
        bar.SetBuildState(BuildState.Running);
        Assert.Equal("Running", bar.BuildStateText);
        bar.SetBuildState(BuildState.Idle);
        Assert.Equal("", bar.BuildStateText);

        Assert.Contains(nameof(StatusBarViewModel.BuildStateText), changed);
    }

    [Fact]
    public void BusyAppearsAfterTheDelayAndEndsWithItsScope()
    {
        var bar = new StatusBarViewModel(time, new InlineDispatcher());

        IDisposable scope = bar.BeginBusy("Loading project…");
        time.Advance(StatusBarViewModel.BusyIndicatorDelay - TimeSpan.FromMilliseconds(1));
        Assert.False(bar.IsBusy);

        time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.True(bar.IsBusy);
        Assert.Equal("Loading project…", bar.BusyText);

        scope.Dispose();
        scope.Dispose();
        Assert.False(bar.IsBusy);
        Assert.Null(bar.BusyText);
    }

    [Fact]
    public void AnOperationFasterThanTheDelayNeverShowsBusy()
    {
        var bar = new StatusBarViewModel(time, new InlineDispatcher());
        var changed = new List<string?>();
        bar.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        bar.BeginBusy("Quick").Dispose();
        time.Advance(TimeSpan.FromSeconds(1));

        Assert.False(bar.IsBusy);
        Assert.DoesNotContain(nameof(StatusBarViewModel.IsBusy), changed);
    }

    [Fact]
    public void NestedScopesStayBusyUntilTheLastEndsAndShowTheNewestText()
    {
        var bar = new StatusBarViewModel(time, new InlineDispatcher());
        IDisposable outer = bar.BeginBusy("Loading references…");
        IDisposable inner = bar.BeginBusy("Preparing graphs…");
        time.Advance(StatusBarViewModel.BusyIndicatorDelay);
        Assert.Equal("Preparing graphs…", bar.BusyText);

        inner.Dispose();
        Assert.True(bar.IsBusy);
        Assert.Equal("Loading references…", bar.BusyText);

        outer.Dispose();
        Assert.False(bar.IsBusy);
    }

    [Fact]
    public void BuildingIsBusyAtOnceWithItsOwnText()
    {
        var bar = new StatusBarViewModel(time, new InlineDispatcher());

        bar.SetBuildState(BuildState.Building);
        Assert.True(bar.IsBusy);
        Assert.Equal("Building…", bar.BusyText);

        bar.SetBuildState(BuildState.Running);
        Assert.False(bar.IsBusy);
        Assert.Null(bar.BusyText);
    }

    [Fact]
    public async Task DisposingWhileOperationsBeginAndEndOnOtherThreadsNeverThrows()
    {
        var bar = new StatusBarViewModel(time, new InlineDispatcher());
        using var stop = new CancellationTokenSource();
        Task[] workers = [.. Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            while (!stop.IsCancellationRequested)
            {
                bar.BeginBusy("work").Dispose();
            }
        }, TestContext.Current.CancellationToken))];

        for (int i = 0; i < 20_000; i++)
        {
            bar.Dispose();
        }

        await stop.CancelAsync();
        await Task.WhenAll(workers);
    }

    [Fact]
    public void TheBuiltInSegmentsAreStatusBarItemsOfTheEditorOnTheirSides()
    {
        ContributionRegistry registry = BuiltInRegistry();

        Assert.Equal(
            new Dictionary<string, StatusBarAlignment>
            {
                ["netprints.status.errors"] = StatusBarAlignment.Left,
                ["netprints.status.warnings"] = StatusBarAlignment.Left,
                ["netprints.status.selection"] = StatusBarAlignment.Left,
                ["netprints.status.build"] = StatusBarAlignment.Right,
                ["netprints.status.run"] = StatusBarAlignment.Right,
                ["netprints.status.zoom"] = StatusBarAlignment.Right,
            },
            registry.StatusBarItems.ToDictionary(item => item.Id, item => item.Alignment));
        Assert.All(registry.StatusBarItems, item => Assert.Equal(BuiltInContributions.OwnerId, registry.OwnerOf(item.Id)));
    }

    [Fact]
    public void ABarShowsItsItemsOnTheLeftAndTheRightByOrder()
    {
        var bar = new StatusBarViewModel(time, new InlineDispatcher());
        StatusBarItemDescriptor[] items =
        [
            Item("test.status.a", StatusBarAlignment.Right, 2),
            Item("test.status.b", StatusBarAlignment.Right, 1),
            Item("test.status.c", StatusBarAlignment.Left, 5),
            Item("test.status.d", StatusBarAlignment.Left, 3),
        ];

        bar.Attach(items, _ => new StubContext());

        Assert.Equal(["test.status.b", "test.status.a"], bar.RightItems.Select(item => item.Id));
        Assert.Equal(["test.status.d", "test.status.c"], bar.LeftItems.Select(item => item.Id));
    }

    [Fact]
    public void AnItemWhoseAppliesToIsFalseIsHiddenUntilItApplies()
    {
        var bar = new StatusBarViewModel(time, new InlineDispatcher());
        bool applies = false;

        bar.Attach([Item("test.status.a", StatusBarAlignment.Right, 0, _ => applies)], _ => new StubContext());

        Assert.Empty(bar.RightItems);

        applies = true;
        bar.Refresh();

        Assert.Equal("test.status.a", Assert.Single(bar.RightItems).Id);

        applies = false;
        bar.Refresh();

        Assert.Empty(bar.RightItems);
    }

    [Fact]
    public void TheBuildAndRunSegmentsFollowTheBuildState()
    {
        ContributionRegistry registry = BuiltInRegistry();
        using var shell = new ShellViewModel(registry, PanelFactoryContext.Detached, time, new InlineDispatcher());
        StatusBarViewModel bar = shell.StatusBar;

        Assert.Equal("", SegmentText(bar, "netprints.status.build"));
        Assert.Null(Find(bar, "netprints.status.run"));

        bar.SetBuildState(BuildState.Building);
        Assert.Equal("Building…", SegmentText(bar, "netprints.status.build"));
        Assert.Null(Find(bar, "netprints.status.run"));

        bar.SetBuildState(BuildState.Running);
        Assert.Equal("", SegmentText(bar, "netprints.status.build"));
        Assert.Equal("Running", SegmentText(bar, "netprints.status.run"));
    }

    private static ContributionRegistry BuiltInRegistry()
    {
        var registry = new ContributionRegistry(NullLogger<ContributionRegistry>.Instance);
        BuiltInContributions.Register(registry);
        registry.Freeze();
        return registry;
    }

    private static StatusBarItemViewModel? Find(StatusBarViewModel bar, string id) =>
        bar.LeftItems.Concat(bar.RightItems).FirstOrDefault(item => item.Id == id);

    private static string? SegmentText(StatusBarViewModel bar, string id) =>
        Assert.IsAssignableFrom<StatusSegmentViewModel>(Assert.IsType<StatusBarItemViewModel>(Find(bar, id)).Content).Text;

    private static StatusBarItemDescriptor Item(string id, StatusBarAlignment alignment, int order, Func<IStatusBarContext, bool>? appliesTo = null) =>
        new(id, alignment, order, _ => new object(), appliesTo);

    private sealed class StubContext : IStatusBarContext
    {
        public IEditorShell Shell => PanelFactoryContext.Detached.Shell;

        public IProjectSession? Session => null;

        public IGraphEditor? ActiveGraph => null;

        public IServiceProvider Services => EmptyServiceProvider.Instance;
    }
}
