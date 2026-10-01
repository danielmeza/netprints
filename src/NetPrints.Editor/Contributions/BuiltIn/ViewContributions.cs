using NetPrints.Editor.Commands;

namespace NetPrints.Editor.Contributions.BuiltIn;

/// <summary>The built-in commands of the View menu (contracts/commands.md section 1).</summary>
public static class ViewContributions
{
    private const string MenuName = "View";
    private const string ViewportGroup = "viewport";

    /// <summary>Registers the View menu's commands.</summary>
    /// <param name="registry">The registry to add to.</param>
    public static void Register(IContributionRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        registry.AddCommand(new CommandDescriptor(
            ContributionIds.CommandPrefix + "frameSelection",
            "Frame selection",
            new FrameSelectionCommandHandler(),
            IconKind: "CropFree",
            DefaultGestures: ["F"],
            Scope: CommandScope.Graph,
            Menu: new MenuPlacement(MenuName, ViewportGroup, 0),
            CommandBarOrder: null));

        registry.AddCommand(new CommandDescriptor(
            ContributionIds.CommandPrefix + "fitAll",
            "Fit all",
            new FitAllCommandHandler(),
            IconKind: "FitToScreen",
            DefaultGestures: ["Home", "Shift+F"],
            Scope: CommandScope.Graph,
            Menu: new MenuPlacement(MenuName, ViewportGroup, 1),
            CommandBarOrder: null));
    }
}
