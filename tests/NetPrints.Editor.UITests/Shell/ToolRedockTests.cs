using Avalonia.Headless.XUnit;
using Dock.Model;
using Dock.Model.Controls;
using Dock.Model.Core;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Shell.Docking;
using NetPrints.Editor.UITests.Hosting;

namespace NetPrints.Editor.UITests.Shell;

/// <summary>Owner feedback 2026-10-15: docking a floated pane anywhere must not duplicate it or the panes it started with.</summary>
public class ToolRedockTests
{
    private static readonly string[] BottomIds = [PanelContributions.ErrorsId, PanelContributions.OutputId, PanelContributions.CSharpId];

    /// <summary>Where a floated pane is dropped.</summary>
    public enum Drop
    {
        MoveIntoGroup,
        SwapWithGroupTab,
        SplitLeft,
        SplitRight,
        SplitTop,
        SplitBottom,
        GlobalTarget,
    }

    private static List<string> ToolIds(ShellRig rig) =>
        [.. ShellDockFactory.Walk(rig.Adapter.Layout).OfType<ShellTool>().Select(tool => tool.Id)];

    private static ShellTool Tool(ShellRig rig, string panelId) =>
        ShellDockFactory.Walk(rig.Adapter.Layout).OfType<ShellTool>().Single(t => t.Id == panelId);

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
        ShellTool tool = Tool(rig, panelId);
        Assert.True(ShellDockFactory.IsFloating(rig.Adapter.Layout, tool));
        IDock source = Assert.IsAssignableFrom<IDock>(tool.Owner);
        IToolDock bottom = Assert.IsAssignableFrom<IToolDock>(rig.Adapter.DockFactory.DefaultDockOf(PanelDock.Bottom));

        rig.Adapter.DockFactory.MoveDockable(source, bottom, tool, null);
        rig.Settle();

        Assert.Equal(expected, ToolIds(rig).Order());
        Assert.Equal(BottomIds.Order(), (bottom.VisibleDockables ?? []).Select(dockable => dockable.Id).Order());
    }

    [AvaloniaTheory(Timeout = TestAppBuilder.Timeout)]
    [InlineData(Drop.MoveIntoGroup)]
    [InlineData(Drop.SwapWithGroupTab)]
    [InlineData(Drop.SplitLeft)]
    [InlineData(Drop.SplitRight)]
    [InlineData(Drop.SplitTop)]
    [InlineData(Drop.SplitBottom)]
    [InlineData(Drop.GlobalTarget)]
    public void FloatedPanesDroppedAnywhereAppearOnceWhereTheyWereDropped(Drop drop)
    {
        using var rig = ShellRig.Create();
        rig.Settle();
        string[] expected = [.. ToolIds(rig).Order()];
        ShellDockFactory factory = rig.Adapter.DockFactory;
        IRootDock layout = rig.Adapter.Layout;
        var manager = new DockManager(new DockService());

        rig.Adapter.FloatPanel(PanelContributions.OutputId);
        rig.Settle();
        rig.Adapter.FloatPanel(PanelContributions.ErrorsId);
        rig.Settle();
        IToolDock bottom = Assert.IsAssignableFrom<IToolDock>(factory.DefaultDockOf(PanelDock.Bottom));
        ShellTool csharp = Tool(rig, PanelContributions.CSharpId);
        ShellTool output = Tool(rig, PanelContributions.OutputId);
        ShellTool errors = Tool(rig, PanelContributions.ErrorsId);
        Assert.True(ShellDockFactory.IsFloating(layout, output));
        Assert.True(ShellDockFactory.IsFloating(layout, errors));

        foreach (ShellTool tool in drop is Drop.SwapWithGroupTab ? new[] { output } : new[] { output, errors })
        {
            IDock source = Assert.IsAssignableFrom<IDock>(tool.Owner);
            switch (drop)
            {
                case Drop.MoveIntoGroup:
                    factory.MoveDockable(source, bottom, tool, null);
                    break;
                case Drop.SwapWithGroupTab:
                    factory.SwapDockable(source, bottom, tool, csharp);
                    break;
                case Drop.SplitLeft:
                    Assert.True(manager.ValidateTool(tool, bottom, DragAction.Move, DockOperation.Left, true));
                    break;
                case Drop.SplitRight:
                    Assert.True(manager.ValidateTool(tool, bottom, DragAction.Move, DockOperation.Right, true));
                    break;
                case Drop.SplitTop:
                    Assert.True(manager.ValidateTool(tool, bottom, DragAction.Move, DockOperation.Top, true));
                    break;
                case Drop.SplitBottom:
                    Assert.True(manager.ValidateTool(tool, bottom, DragAction.Move, DockOperation.Bottom, true));
                    break;
                case Drop.GlobalTarget:
                    Assert.True(manager.ValidateTool(tool, layout, DragAction.Move, DockOperation.Fill, true));
                    break;
            }

            rig.Settle();
        }

        Assert.Equal(expected, ToolIds(rig).Order());
        Assert.False(ShellDockFactory.IsFloating(layout, output));
        Assert.Equal(drop is Drop.SwapWithGroupTab, ShellDockFactory.IsFloating(layout, errors));
        if (drop is Drop.MoveIntoGroup)
        {
            Assert.Equal(BottomIds.Order(), (bottom.VisibleDockables ?? []).Select(dockable => dockable.Id).Order());
        }
        else if (drop is not Drop.SwapWithGroupTab)
        {
            Assert.DoesNotContain(PanelContributions.OutputId, (bottom.VisibleDockables ?? []).Select(dockable => dockable.Id));
        }
    }
}
