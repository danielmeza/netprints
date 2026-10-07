using System.Text.Json;
using Avalonia.Headless.XUnit;
using Dock.Model.Controls;
using Dock.Model.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Shell.Docking;
using NetPrints.Editor.State;

namespace NetPrints.Editor.UITests.Shell;

/// <summary>The dock layout saved through the ADR-0018 envelope and restored into a fresh adapter (T064, FR-050).</summary>
public class DockLayoutRoundTripTests
{
    private static readonly DocumentId A = DocumentId.Graph("A.cs", "method:1");
    private static readonly DocumentId B = DocumentId.Graph("A.cs", "method:2");
    private static readonly DocumentId C = DocumentId.Graph("A.cs", "method:3");

    private static string Json(DockShellAdapter adapter) => JsonSerializer.Serialize(adapter.CaptureLayout(), DockJsonContext.Default.DockLayoutDto);

    private static LayoutState Save(DockShellAdapter adapter) => LayoutSerializer.ToState(adapter.CaptureLayout());

    private static IEnumerable<IDockable> Walk(ShellRig rig) => ShellDockFactory.Walk(rig.Adapter.Layout);

    private static DockNodeDto Rename(DockNodeDto node, string from, string to) =>
        node with { Id = node.Id == from ? to : node.Id, ActiveId = node.ActiveId == from ? to : node.ActiveId, Children = node.Children?.Select(child => Rename(child, from, to)).ToList() };

    private static DockNodeDto? Find(DockNodeDto? node, string id) =>
        node is null ? null : node.Id == id ? node : node.Children?.Select(child => Find(child, id)).OfType<DockNodeDto>().FirstOrDefault();

    private static LayoutState Edit(LayoutState state, Func<DockLayoutDto, DockLayoutDto> change)
    {
        DockLayoutDto dto = state.DockLayout?.Deserialize(DockJsonContext.Default.DockLayoutDto) ?? throw new InvalidOperationException("No layout.");
        return LayoutSerializer.ToState(change(dto));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void TheDefaultLayoutRoundTripsToTheSameLayout()
    {
        using var first = ShellRig.Create();
        first.Settle();
        LayoutState saved = Save(first.Adapter);
        Assert.Equal(StateFile.CurrentVersion, saved.SchemaVersion);
        Assert.Equal("dock", saved.Engine);

        using var second = ShellRig.Create();
        second.Adapter.RestoreLayout(saved);
        second.Settle();

        Assert.Equal(Json(first.Adapter), Json(second.Adapter));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void AChangedLayoutIsRestoredPanelsDocumentsWindowsProportionsAndTheActiveDocument()
    {
        using var first = ShellRig.Create();
        first.Api.OpenDocument(A);
        first.Api.OpenDocument(B);
        first.Api.OpenDocument(C);
        first.Api.HidePanel(PanelContributions.InspectorId);
        first.Api.FloatDocument(B);
        first.Adapter.FloatPanel(PanelContributions.ProjectTreeId);
        first.Api.ActivateDocument(A);
        first.Settle();
        IDockable leftDock = Walk(first).First(dock => dock.Id == "netprints.dock.left");
        leftDock.Proportion = 0.31;
        first.Settle();
        LayoutState saved = Save(first.Adapter);
        double savedLeft = Find(saved.DockLayout?.Deserialize(DockJsonContext.Default.DockLayoutDto)?.Root, "netprints.dock.left")?.Proportion ?? double.NaN;

        using var second = ShellRig.Create();
        second.Adapter.RestoreLayout(saved);
        second.Settle();

        Assert.Equal([A, C, B], second.Api.OpenDocuments);
        Assert.Equal(A, second.Api.ActiveDocument);
        Assert.Equal(A, second.Shell.ActiveDocument?.Id);
        Assert.True(second.Api.IsFloating(B));
        Assert.False(second.Api.IsFloating(A));
        Assert.False(second.Api.IsPanelVisible(PanelContributions.InspectorId));
        Assert.False(second.Shell.Panels.Single(panel => panel.Id == PanelContributions.InspectorId).IsVisible);
        Assert.True(second.Api.IsPanelVisible(PanelContributions.ProjectTreeId));
        Assert.True(ShellDockFactory.IsFloating(second.Adapter.Layout, Walk(second).First(tool => tool.Id == PanelContributions.ProjectTreeId)));
        Assert.InRange(savedLeft, 0.25, 0.35);
        Assert.Equal(savedLeft, Walk(second).First(dock => dock.Id == "netprints.dock.left").Proportion, 3);
        Assert.Equal(3, second.Shell.Documents.Count);
        Assert.NotNull(second.FindOne(ShellRig.Content(A)));

        second.Api.ShowPanel(PanelContributions.InspectorId);
        Assert.True(second.Api.IsPanelVisible(PanelContributions.InspectorId));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void APanelNoLongerRegisteredIsDroppedAndTheRestIsRestored()
    {
        using var first = ShellRig.Create();
        first.Api.HidePanel(PanelContributions.OutputId);
        first.Settle();
        LayoutState saved = Edit(Save(first.Adapter), dto => dto with
        {
            Root = Rename(dto.Root, PanelContributions.InspectorId, "acme.panel.gone"),
            Hidden = [.. (dto.Hidden ?? []).Select(id => id == PanelContributions.OutputId ? "acme.panel.hidden" : id), PanelContributions.OutputId],
        });

        using var second = ShellRig.Create();
        second.Adapter.RestoreLayout(saved);
        second.Settle();

        Assert.DoesNotContain(Walk(second), dockable => dockable.Id == "acme.panel.gone");
        Assert.DoesNotContain(second.Adapter.Layout.HiddenDockables ?? [], dockable => dockable.Id == "acme.panel.hidden");
        Assert.False(second.Api.IsPanelVisible("acme.panel.gone"));
        Assert.False(second.Api.IsPanelVisible(PanelContributions.OutputId));
        Assert.True(second.Api.IsPanelVisible(PanelContributions.ProjectTreeId));
        Assert.True(second.Api.IsPanelVisible(PanelContributions.ErrorsId));
        Assert.True(second.Api.IsPanelVisible(PanelContributions.InspectorId));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void AGraphThatNoLongerExistsIsDroppedAndTheOthersAreRestored()
    {
        using var first = ShellRig.Create();
        first.Api.OpenDocument(A);
        first.Api.OpenDocument(B);
        first.Api.FloatDocument(B);
        first.Api.OpenDocument(C);
        first.Settle();
        LayoutState saved = Save(first.Adapter);

        using var second = ShellRig.Create(id => id == B ? null : new TestDocumentViewModel(id, id.ToString()));
        second.Adapter.RestoreLayout(saved);
        second.Settle();

        Assert.Equal([A, C], second.Api.OpenDocuments);
        Assert.Equal(C, second.Api.ActiveDocument);
        Assert.Single(second.Ui.Tree.Windows);
        Assert.Equal(2, second.Shell.Documents.Count);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void ADocumentAlreadyOpenIsKeptAndPlacedByTheLayout()
    {
        using var first = ShellRig.Create();
        first.Api.OpenDocument(A);
        first.Api.OpenDocument(B);
        first.Api.FloatDocument(B);
        first.Settle();
        LayoutState saved = Save(first.Adapter);

        using var second = ShellRig.Create();
        second.Api.OpenDocument(B);
        DocumentViewModel open = second.Shell.Documents.Single();
        second.Adapter.RestoreLayout(saved);
        second.Settle();

        Assert.Same(open, second.Shell.FindDocument(B));
        Assert.True(second.Api.IsFloating(B));
        Assert.Equal(2, second.Shell.Documents.Count);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void ALayoutThatCannotBeReadIsLoggedAndTheDefaultLayoutStays()
    {
        var logger = new RecordingLogger();
        using var rig = ShellRig.Create(logger: logger);
        rig.Api.OpenDocument(A);
        rig.Api.HidePanel(PanelContributions.InspectorId);
        rig.Settle();
        string before = Json(rig.Adapter);

        rig.Adapter.RestoreLayout(new LayoutState(StateFile.CurrentVersion, "dock", JsonDocument.Parse("""{ "root": 5, "windows": "no" }""").RootElement));
        rig.Settle();

        Assert.Equal(1210, Assert.Single(logger.Ids));
        Assert.Equal(before, Json(rig.Adapter));
        Assert.Equal([A], rig.Api.OpenDocuments);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void ALayoutOfAnotherEngineOrWithoutATreeIsLoggedAndIgnored()
    {
        var logger = new RecordingLogger();
        using var rig = ShellRig.Create(logger: logger);
        LayoutState saved = Save(rig.Adapter);

        rig.Adapter.RestoreLayout(saved with { Engine = "grid" });
        rig.Adapter.RestoreLayout(saved with { DockLayout = null });
        rig.Adapter.RestoreLayout(null);
        rig.Settle();

        Assert.Equal([1210, 1210], logger.Ids);
        Assert.All(new[] { PanelContributions.ProjectTreeId, PanelContributions.InspectorId, PanelContributions.ErrorsId }, id => Assert.True(rig.Api.IsPanelVisible(id), id));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void ALayoutThatCannotBeBuiltFallsBackToTheDefaultAndKeepsTheDocuments()
    {
        var logger = new RecordingLogger();
        using var rig = ShellRig.Create(logger: logger);
        rig.Api.OpenDocument(A);
        rig.Settle();
        LayoutState saved = Edit(Save(rig.Adapter), dto => dto with { Root = dto.Root with { Kind = "nonsense" } });

        rig.Adapter.RestoreLayout(saved);
        rig.Settle();

        Assert.Equal(1210, Assert.Single(logger.Ids));
        Assert.Equal([A], rig.Api.OpenDocuments);
        Assert.True(rig.Api.IsPanelVisible(PanelContributions.ErrorsId));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void ChangesAreSavedOnceAfterTheDelayAndEachChangeRestartsIt()
    {
        using var rig = ShellRig.Create();
        var time = new FakeTimeProvider();
        var store = new LayoutStore();
        using var saver = new LayoutSaver(rig.Adapter, store, time, new InlineDispatcher(), TimeSpan.FromSeconds(1));

        rig.Api.HidePanel(PanelContributions.InspectorId);
        time.Advance(TimeSpan.FromMilliseconds(600));
        Assert.Empty(store.Saved);
        rig.Api.HidePanel(PanelContributions.ErrorsId);
        time.Advance(TimeSpan.FromMilliseconds(600));
        Assert.Empty(store.Saved);
        time.Advance(TimeSpan.FromMilliseconds(500));

        LayoutState saved = Assert.Single(store.Saved);
        time.Advance(TimeSpan.FromMinutes(1));
        Assert.Single(store.Saved);

        using var second = ShellRig.Create();
        second.Adapter.RestoreLayout(saved);
        second.Settle();
        Assert.False(second.Api.IsPanelVisible(PanelContributions.InspectorId));
        Assert.False(second.Api.IsPanelVisible(PanelContributions.ErrorsId));
        Assert.True(second.Api.IsPanelVisible(PanelContributions.ProjectTreeId));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void FlushSavesAPendingChangeAtOnceAndNothingElse()
    {
        using var rig = ShellRig.Create();
        var time = new FakeTimeProvider();
        var store = new LayoutStore();
        using var saver = new LayoutSaver(rig.Adapter, store, time, new InlineDispatcher(), TimeSpan.FromSeconds(1));

        saver.Flush();
        Assert.Empty(store.Saved);

        rig.Api.HidePanel(PanelContributions.InspectorId);
        saver.Flush();
        Assert.Single(store.Saved);

        time.Advance(TimeSpan.FromMinutes(1));
        Assert.Single(store.Saved);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void SaveNowSavesEvenWhenNothingChanged()
    {
        using var rig = ShellRig.Create();
        var store = new LayoutStore();
        using var saver = new LayoutSaver(rig.Adapter, store, new FakeTimeProvider(), new InlineDispatcher(), TimeSpan.FromSeconds(1));

        saver.SaveNow();

        Assert.Single(store.Saved);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void ADisposedSaverSavesNothing()
    {
        using var rig = ShellRig.Create();
        var time = new FakeTimeProvider();
        var store = new LayoutStore();
        var saver = new LayoutSaver(rig.Adapter, store, time, new InlineDispatcher(), TimeSpan.FromSeconds(1));

        rig.Api.HidePanel(PanelContributions.InspectorId);
        saver.Dispose();
        time.Advance(TimeSpan.FromMinutes(1));

        Assert.Empty(store.Saved);
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

    private sealed class LayoutStore : IEditorStateStore
    {
        public List<LayoutState> Saved { get; } = [];

        public void SaveLayout(LayoutState state, bool userChanged = false) => Saved.Add(state);

        public LayoutState? LoadLayout() => Saved.Count > 0 ? Saved[^1] : null;

        public WindowState? LoadWindow() => throw new NotSupportedException();

        public void SaveWindow(WindowState state) => throw new NotSupportedException();

        public RecentState LoadRecent() => throw new NotSupportedException();

        public void SaveRecent(RecentState state) => throw new NotSupportedException();

        public SessionState? LoadSession(string projectPath) => throw new NotSupportedException();

        public void SaveSession(SessionState state) => throw new NotSupportedException();

        public StartState? LoadStart() => throw new NotSupportedException();

        public void SaveStart(StartState state, bool userChanged = false) => throw new NotSupportedException();
    }

    private sealed class RecordingLogger : ILogger
    {
        public List<int> Ids { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Ids.Add(eventId.Id);
    }
}
