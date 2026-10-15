using System.Text.Json;
using Avalonia.Headless.XUnit;
using Dock.Model;
using Dock.Model.Controls;
using Dock.Model.Core;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Shell.Docking;
using NetPrints.Editor.State;

namespace NetPrints.Editor.UITests.Shell;

/// <summary>Panels the user dragged into new splits and windows are suspended with the project and come back with it (E-F9).</summary>
public class RearrangedPanelsSuspensionTests
{
    private static ShellTool Tool(ShellRig rig, string id) => Tool(rig.Adapter, id);

    private static ShellTool Tool(DockShellAdapter adapter, string id) => ShellDockFactory.Walk(adapter.Layout).OfType<ShellTool>().First(tool => tool.Id == id);

    private static IDock OwnerOf(IDockable dockable) => dockable.Owner as IDock ?? throw new InvalidOperationException("The panel has no dock.");

    private static void Rearrange(ShellRig rig)
    {
        Rearrange(rig.Adapter);
        rig.Settle();
    }

    internal static void Rearrange(DockShellAdapter adapter)
    {
        ShellDockFactory factory = adapter.DockFactory;
        var service = new DockService();
        IToolDock Dock(string id) => ShellDockFactory.Walk(adapter.Layout).OfType<IToolDock>().First(dock => dock.Id == id);
        ShellTool tree = Tool(adapter, PanelContributions.ProjectTreeId);
        service.SplitDockable(tree, OwnerOf(tree), Dock("netprints.dock.bottom"), DockOperation.Right, true);
        ShellTool inspector = Tool(adapter, PanelContributions.InspectorId);
        service.SplitDockable(inspector, OwnerOf(inspector), Dock("netprints.dock.bottom"), DockOperation.Left, true);
        ShellTool variables = Tool(adapter, PanelContributions.VariablesId);
        service.MoveDockable(variables, OwnerOf(variables), Dock("netprints.dock.bottom"), true);
        factory.FloatDockable(Tool(adapter, PanelContributions.OutputId));
        factory.FloatDockable(Tool(adapter, PanelContributions.ErrorsId));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void RearrangedPanelsAreSuspendedAndResumed()
    {
        using var rig = ShellRig.Create();
        rig.Settle();
        Rearrange(rig);

        Exception? thrown = Record.Exception(() => rig.Adapter.SetPanelsSuspended(true));
        Assert.Null(thrown);
        rig.Settle();
        Assert.All(rig.Shell.Panels, panel => Assert.False(rig.Api.IsPanelVisible(panel.Id)));

        rig.Adapter.SetPanelsSuspended(false);
        rig.Settle();
        Assert.All(rig.Shell.Panels, panel => Assert.True(rig.Api.IsPanelVisible(panel.Id)));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void EveryDockOfARearrangedLayoutHasAUniqueId()
    {
        using var rig = ShellRig.Create();
        rig.Settle();
        Rearrange(rig);

        AssertUniqueDockIds(rig);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void ResumingKeepsEveryPanelAndTheDocksTheyWereIn()
    {
        using var rig = ShellRig.Create();
        rig.Settle();
        Rearrange(rig);
        var floating = rig.Shell.Panels.ToDictionary(panel => panel.Id, panel => ShellDockFactory.IsFloating(rig.Adapter.Layout, Tool(rig, panel.Id)));
        string tree = Tool(rig, PanelContributions.ProjectTreeId).Owner?.Id ?? "";
        string inspector = Tool(rig, PanelContributions.InspectorId).Owner?.Id ?? "";

        rig.Adapter.SetPanelsSuspended(true);
        rig.Settle();
        rig.Adapter.SetPanelsSuspended(false);
        rig.Settle();

        Assert.All(rig.Shell.Panels, panel => Assert.True(rig.Api.IsPanelVisible(panel.Id)));
        Assert.Equal(tree, Tool(rig, PanelContributions.ProjectTreeId).Owner?.Id);
        Assert.Equal(inspector, Tool(rig, PanelContributions.InspectorId).Owner?.Id);
        Assert.Equal(floating[PanelContributions.ProjectTreeId], ShellDockFactory.IsFloating(rig.Adapter.Layout, Tool(rig, PanelContributions.ProjectTreeId)));
        AssertUniqueDockIds(rig);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void ASuspendedAndResumedArrangementCanBeSuspendedAgain()
    {
        using var rig = ShellRig.Create();
        rig.Settle();
        Rearrange(rig);

        for (int round = 0; round < 3; round++)
        {
            rig.Adapter.SetPanelsSuspended(true);
            rig.Settle();
            Assert.All(rig.Shell.Panels, panel => Assert.False(rig.Api.IsPanelVisible(panel.Id)));
            rig.Adapter.SetPanelsSuspended(false);
            rig.Settle();
            Assert.All(rig.Shell.Panels, panel => Assert.True(rig.Api.IsPanelVisible(panel.Id)));
        }
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void ARearrangedLayoutRoundTripsThroughSaveAndRestoreAndSuspends()
    {
        using var first = ShellRig.Create();
        first.Settle();
        Rearrange(first);
        string saved = Json(first);

        using var second = ShellRig.Create();
        second.Adapter.RestoreLayout(Save(first));
        second.Settle();

        Assert.All(second.Shell.Panels, panel => Assert.True(second.Api.IsPanelVisible(panel.Id)));
        AssertUniqueDockIds(second);
        Assert.Equal(saved, Json(second));
        Assert.Null(Record.Exception(() => second.Adapter.SetPanelsSuspended(true)));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void ASavedLayoutWhoseDocksHaveNoIdOrTheSameIdStillRestoresAndSuspends()
    {
        using var first = ShellRig.Create();
        first.Settle();
        HashSet<string> defaults = [.. ShellDockFactory.Walk(first.Adapter.Layout).OfType<IDock>().Select(dock => dock.Id)];
        Rearrange(first);
        LayoutState state = Save(first);
        DockLayoutDto dto = state.DockLayout?.Deserialize(DockJsonContext.Default.DockLayoutDto) ?? throw new InvalidOperationException("No layout.");
        LayoutState legacy = LayoutSerializer.ToState(dto with { Root = Blank(dto.Root, defaults), Windows = [.. (dto.Windows ?? []).Select(window => window with { Root = Blank(window.Root, defaults) })] });

        using var second = ShellRig.Create();
        second.Adapter.RestoreLayout(legacy);
        second.Settle();

        Assert.All(second.Shell.Panels, panel => Assert.True(second.Api.IsPanelVisible(panel.Id)));
        AssertUniqueDockIds(second);
        Assert.Null(Record.Exception(() => second.Adapter.SetPanelsSuspended(true)));
        second.Adapter.SetPanelsSuspended(false);
        Assert.All(second.Shell.Panels, panel => Assert.True(second.Api.IsPanelVisible(panel.Id)));
    }

    private static DockNodeDto Blank(DockNodeDto node, HashSet<string> defaults) =>
        node.Kind is DockNodeKinds.Tool or DockNodeKinds.Document or DockNodeKinds.Splitter
            ? node
            : node with { Id = defaults.Contains(node.Id) ? node.Id : "", Children = node.Children?.Select(child => Blank(child, defaults)).ToList() };

    private static string Json(ShellRig rig) => JsonSerializer.Serialize(rig.Adapter.CaptureLayout(), DockJsonContext.Default.DockLayoutDto);

    private static LayoutState Save(ShellRig rig) => LayoutSerializer.ToState(rig.Adapter.CaptureLayout());

    private static void AssertUniqueDockIds(ShellRig rig)
    {
        List<string> ids = [.. ShellDockFactory.Walk(rig.Adapter.Layout).OfType<IDock>().Select(dock => dock.Id)];
        Assert.All(ids, id => Assert.False(string.IsNullOrEmpty(id)));
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }
}
