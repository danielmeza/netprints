using System.Text.Json;
using Avalonia.Headless.XUnit;
using Dock.Model.Core;
using Microsoft.Extensions.Time.Testing;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Shell.Docking;
using NetPrints.Editor.State;
using NetPrints.Editor.UITests.Driving;
using NetPrints.Editor.UITests.Hosting;

namespace NetPrints.Editor.UITests.Shell;

/// <summary>The tool panels are hidden while no project is open, come back with it, and the hidden state is never saved (FR-046).</summary>
public class NoProjectPanelsTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string OwnerOf(ShellRig rig, string panelId) =>
        ShellDockFactory.Walk(rig.Adapter.Layout).First(dockable => dockable.Id == panelId).Owner?.Id ?? "";

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void SuspendingHidesEveryPanelAndResumingBringsThemBackToTheirDocks()
    {
        using var rig = ShellRig.Create();
        rig.Settle();
        var owners = rig.Shell.Panels.ToDictionary(panel => panel.Id, panel => OwnerOf(rig, panel.Id));
        IDockable bottom = ShellDockFactory.Walk(rig.Adapter.Layout).First(dock => dock.Id == "netprints.dock.bottom");
        string? active = (bottom as IDock)?.ActiveDockable?.Id;

        rig.Adapter.SetPanelsSuspended(true);
        rig.Settle();

        Assert.True(rig.Adapter.PanelsSuspended);
        Assert.All(rig.Shell.Panels, panel => Assert.False(panel.IsVisible));
        Assert.All(rig.Shell.Panels, panel => Assert.False(rig.Api.IsPanelVisible(panel.Id)));

        rig.Adapter.SetPanelsSuspended(false);
        rig.Settle();

        Assert.False(rig.Adapter.PanelsSuspended);
        Assert.All(rig.Shell.Panels, panel => Assert.True(panel.IsVisible));
        Assert.All(rig.Shell.Panels, panel => Assert.Equal(owners[panel.Id], OwnerOf(rig, panel.Id)));
        Assert.Equal(active, (bottom as IDock)?.ActiveDockable?.Id);
        Assert.Equal(rig.Shell.Panels.Where(panel => owners[panel.Id] == "netprints.dock.bottom").OrderBy(panel => panel.Order).Select(panel => panel.Id),
            (((IDock)bottom).VisibleDockables ?? []).Select(dockable => dockable.Id));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void APanelTheUserHidIsStillHiddenAfterTheSuspension()
    {
        using var rig = ShellRig.Create();
        rig.Api.HidePanel(PanelContributions.InspectorId);

        rig.Adapter.SetPanelsSuspended(true);
        rig.Adapter.SetPanelsSuspended(false);
        rig.Settle();

        Assert.False(rig.Api.IsPanelVisible(PanelContributions.InspectorId));
        Assert.True(rig.Api.IsPanelVisible(PanelContributions.ProjectTreeId));
        Assert.True(rig.Api.IsPanelVisible(PanelContributions.ErrorsId));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void TheWholeLayoutIsTheSameAfterASuspension()
    {
        using var rig = ShellRig.Create();
        rig.Settle();
        string before = JsonSerializer.Serialize(rig.Adapter.CaptureLayout(), DockJsonContext.Default.DockLayoutDto);

        rig.Adapter.SetPanelsSuspended(true);
        rig.Settle();
        rig.Adapter.SetPanelsSuspended(false);
        rig.Settle();

        Assert.Equal(before, JsonSerializer.Serialize(rig.Adapter.CaptureLayout(), DockJsonContext.Default.DockLayoutDto));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void WhileSuspendedTheDocumentsTakeTheWholeWidth()
    {
        using var rig = ShellRig.Create();
        rig.Api.OpenDocument(DocumentId.StartPage);

        rig.Adapter.SetPanelsSuspended(true);
        rig.Settle();

        var documents = ShellDockFactory.Walk(rig.Adapter.Layout).First(dock => dock.Id == ShellDockFactory.DocumentsId);
        var toolDocks = ShellDockFactory.Walk(rig.Adapter.Layout).OfType<Dock.Model.Controls.IToolDock>().Select(dock => dock.Proportion);
        Assert.All(toolDocks, proportion => Assert.Equal(0, proportion));
        Assert.Equal(0, ShellDockFactory.Walk(rig.Adapter.Layout).First(dock => dock.Id == "netprints.right").Proportion);
        Assert.True(documents.Proportion > 0);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void TheDockSizesSurviveTheSuspension()
    {
        using var rig = ShellRig.Create();
        ShellDockFactory.Walk(rig.Adapter.Layout).First(dock => dock.Id == "netprints.dock.left").Proportion = 0.30;
        ShellDockFactory.Walk(rig.Adapter.Layout).First(dock => dock.Id == ShellDockFactory.DocumentsId).Proportion = 0.48;
        rig.Settle();

        rig.Adapter.SetPanelsSuspended(true);
        rig.Settle();
        rig.Adapter.SetPanelsSuspended(false);
        rig.Settle();

        Assert.Equal(0.30, ShellDockFactory.Walk(rig.Adapter.Layout).First(dock => dock.Id == "netprints.dock.left").Proportion, 3);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void AResetLayoutWhileSuspendedKeepsThePanelsHidden()
    {
        using var rig = ShellRig.Create();
        rig.Adapter.SetPanelsSuspended(true);

        rig.Api.ResetLayout();
        rig.Settle();

        Assert.All(rig.Shell.Panels, panel => Assert.False(panel.IsVisible));
        rig.Adapter.SetPanelsSuspended(false);
        Assert.All(rig.Shell.Panels, panel => Assert.True(panel.IsVisible));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void TheLayoutSaverSavesNothingWhileThePanelsAreSuspendedAndTheRestoredLayoutAfterwards()
    {
        using var rig = ShellRig.Create();
        var time = new FakeTimeProvider();
        var store = new CountingStore();
        using var saver = new LayoutSaver(rig.Adapter, store, time, new InlineDispatcher(), TimeSpan.FromSeconds(1));

        rig.Adapter.SetPanelsSuspended(true);
        time.Advance(TimeSpan.FromMinutes(1));
        saver.SaveNow();
        saver.Flush();
        Assert.Empty(store.Saved);

        rig.Adapter.SetPanelsSuspended(false);
        time.Advance(TimeSpan.FromSeconds(2));

        LayoutState saved = Assert.Single(store.Saved);
        using var second = ShellRig.Create();
        second.Adapter.RestoreLayout(saved);
        second.Settle();
        Assert.All(second.Shell.Panels, panel => Assert.True(panel.IsVisible));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task WithNoProjectOpenNoPanelShowsAndTheStartPageDoes()
    {
        var store = new MemoryStateStore();
        await using ShellApp app = ShellApp.Start(store);
        HeadlessDriver.Pump();

        Assert.All(app.Shell.Panels, panel => Assert.False(panel.IsVisible));
        Assert.Equal([DocumentId.StartPage], app.Api.OpenDocuments);

        int saves = store.LayoutSaves;
        app.Window.Close();
        HeadlessDriver.Pump();

        Assert.Equal(saves, store.LayoutSaves);
        AssertSavedLayoutHasEveryPanel(store);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task OpeningAProjectBringsThePanelsBackAndClosingItHidesThemAgain()
    {
        var store = new MemoryStateStore();
        await using ShellApp app = ShellApp.Start(store);
        await app.OpenSampleAsync(Token);
        HeadlessDriver.Pump();

        Assert.All(app.Shell.Panels, panel => Assert.True(panel.IsVisible));
        Assert.All(app.Shell.Panels, panel => Assert.True(app.Api.IsPanelVisible(panel.Id)));

        Assert.True(app.Commands.TryRun(app.Command("closeProject")));
        await Testing.Ui.Driving.UiWait.UntilAsync(app.Driver, () => Task.FromResult(app.Shell.Session is null), "project closed", Token);

        Assert.All(app.Shell.Panels, panel => Assert.False(panel.IsVisible));
        int saves = store.LayoutSaves;
        app.Window.Close();
        HeadlessDriver.Pump();
        Assert.Equal(saves, store.LayoutSaves);
        AssertSavedLayoutHasEveryPanel(store);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task OpeningAProjectRestoresTheDefaultDockSizes()
    {
        await using ShellApp app = ShellApp.Start(new MemoryStateStore());
        await app.OpenSampleAsync(Token);
        HeadlessDriver.Pump();

        var adapter = Assert.IsType<DockShellAdapter>(app.Api);
        double Proportion(string id) => ShellDockFactory.Walk(adapter.Layout).First(dock => dock.Id == id).Proportion;
        Assert.Equal(0.20, Proportion("netprints.dock.left"), 3);
        Assert.Equal(0.22, Proportion("netprints.right"), 3);
        Assert.Equal(0.58, Proportion(ShellDockFactory.DocumentsId), 3);
        Assert.Equal(0.25, Proportion("netprints.dock.bottom"), 3);
        Assert.Equal(0.75, Proportion("netprints.body"), 3);
    }

    private static void AssertSavedLayoutHasEveryPanel(MemoryStateStore store)
    {
        if (store.Layout is not { } saved)
        {
            return;
        }

        using var rig = ShellRig.Create();
        rig.Adapter.RestoreLayout(saved);
        rig.Settle();
        Assert.All(rig.Shell.Panels, panel => Assert.True(rig.Api.IsPanelVisible(panel.Id)));
    }

    private sealed class InlineDispatcher : IUiDispatcher
    {
        public void Post(Action action) => action();

        public Task InvokeAsync(Action action)
        {
            action();
            return Task.CompletedTask;
        }

        public bool CheckAccess() => true;
    }

    private sealed class CountingStore : IEditorStateStore
    {
        public List<LayoutState> Saved { get; } = [];

        public void SaveLayout(LayoutState state) => Saved.Add(state);

        public LayoutState? LoadLayout() => Saved.Count > 0 ? Saved[^1] : null;

        public WindowState? LoadWindow() => throw new NotSupportedException();

        public void SaveWindow(WindowState state) => throw new NotSupportedException();

        public RecentState LoadRecent() => throw new NotSupportedException();

        public void SaveRecent(RecentState state) => throw new NotSupportedException();

        public SessionState? LoadSession(string projectPath) => throw new NotSupportedException();

        public void SaveSession(SessionState state) => throw new NotSupportedException();

        public StartState? LoadStart() => throw new NotSupportedException();

        public void SaveStart(StartState state) => throw new NotSupportedException();
    }
}
