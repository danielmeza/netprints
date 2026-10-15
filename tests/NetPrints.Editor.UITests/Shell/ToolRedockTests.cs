using Avalonia.Headless.XUnit;
using Dock.Model.Controls;
using Dock.Model.Core;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Shell.Docking;
using NetPrints.Editor.UITests.Hosting;

namespace NetPrints.Editor.UITests.Shell;

/// <summary>Owner feedback 2026-10-15: docking a floated pane back must not duplicate it or the panes it started with.</summary>
public class ToolRedockTests
{
    private static readonly string[] BottomIds = [PanelContributions.ErrorsId, PanelContributions.OutputId, PanelContributions.CSharpId];

    private static List<string> ToolIds(ShellRig rig) =>
        [.. ShellDockFactory.Walk(rig.Adapter.Layout).OfType<ShellTool>().Select(tool => tool.Id)];

    [AvaloniaTheory(Timeout = TestAppBuilder.Timeout)]
    [InlineData(PanelContributions.ErrorsId)]
    [InlineData(PanelContributions.OutputId)]
    [InlineData(PanelContributions.CSharpId)]
    public void DockingAFloatedPaneBackKeepsEveryPaneOnceAndTheGroupTogether(string panelId)
    {
        using var rig = ShellRig.Create();
        rig.Settle();
        string[] expected = [.. ToolIds(rig).Order()];

        rig.Adapter.FloatPanel(panelId);
        rig.Settle();
        ShellTool tool = ShellDockFactory.Walk(rig.Adapter.Layout).OfType<ShellTool>().Single(t => t.Id == panelId);
        Assert.True(ShellDockFactory.IsFloating(rig.Adapter.Layout, tool));
        IDock source = Assert.IsAssignableFrom<IDock>(tool.Owner);
        IToolDock bottom = Assert.IsAssignableFrom<IToolDock>(rig.Adapter.DockFactory.DefaultDockOf(PanelDock.Bottom));

        rig.Adapter.DockFactory.MoveDockable(source, bottom, tool, null);
        rig.Settle();

        Assert.Equal(expected, ToolIds(rig).Order());
        Assert.Equal(BottomIds.Order(), (bottom.VisibleDockables ?? []).Select(dockable => dockable.Id).Order());
    }
}
