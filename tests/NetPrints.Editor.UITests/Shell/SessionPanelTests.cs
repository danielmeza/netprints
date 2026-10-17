using Avalonia.Headless.XUnit;
using Dock.Model.Core;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Shell.Docking;

namespace NetPrints.Editor.UITests.Shell;

/// <summary>FR-032, FR-033, T083: a panel of a session group is docked at its default place when the group is added, goes with the group, and a saved layout that names it before it loads is dropped and used again once it loads.</summary>
public class SessionPanelTests
{
    private const string GroupKey = "project";
    private const string PanelId = "acme.tools.panel";
    private const string RightDockId = "netprints.dock.right";

    private static void AddGroup(NetPrints.Editor.Contributions.ContributionRegistry registry) =>
        registry.AddSessionGroup(GroupKey, group => group.For("acme.tools", "Acme Tools").AddPanel(new PanelDescriptor(PanelId, "Acme panel", _ => new object(), PanelDock.Right, 5)));

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void ASessionPanelIsDockedAtItsDefaultPlaceWhenTheGroupIsAddedAndGoesWithIt()
    {
        using SurfaceRig rig = SurfaceRig.Create();
        Assert.Null(rig.Shell.FindPanel(PanelId));

        AddGroup(rig.Registry);
        rig.Settle();

        Assert.NotNull(rig.Shell.FindPanel(PanelId));
        Assert.True(rig.Adapter.IsPanelVisible(PanelId));
        ShellTool tool = Assert.Single(ShellDockFactory.Walk(rig.Adapter.Layout).OfType<ShellTool>(), candidate => candidate.Id == PanelId);
        Assert.Equal(RightDockId, tool.Owner?.Id);

        rig.Registry.RemoveSessionGroup(GroupKey);
        rig.Settle();

        Assert.Null(rig.Shell.FindPanel(PanelId));
        Assert.False(rig.Adapter.IsPanelVisible(PanelId));
        Assert.DoesNotContain(rig.Adapter.Layout.HiddenDockables ?? [], dockable => dockable.Id == PanelId);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void ASavedLayoutThatNamesAPanelNotLoadedDropsItAndThePanelOpensAtItsDefaultPlaceWhenItLoads()
    {
        using SurfaceRig first = SurfaceRig.Create(AddGroup);
        first.Settle();
        Assert.True(first.Adapter.IsPanelVisible(PanelId));
        NetPrints.Editor.State.LayoutState saved = LayoutSerializer.ToState(first.Adapter.CaptureLayout());

        using SurfaceRig second = SurfaceRig.Create();
        second.Adapter.RestoreLayout(saved);
        second.Settle();

        Assert.False(second.Adapter.IsPanelVisible(PanelId));

        AddGroup(second.Registry);
        second.Settle();

        Assert.True(second.Adapter.IsPanelVisible(PanelId));
        ShellTool tool = Assert.Single(ShellDockFactory.Walk(second.Adapter.Layout).OfType<ShellTool>(), candidate => candidate.Id == PanelId);
        Assert.Equal(RightDockId, tool.Owner?.Id);
    }
}
