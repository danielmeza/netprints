using NetPrints.Editor.Commands;

namespace NetPrints.Editor.Contributions.BuiltIn;

/// <summary>The built-in commands of the Go menu; the rest arrive with the navigation tasks (T074 to T077).</summary>
public static class GoContributions
{
    private const string MenuName = "Go";
    private const string TabsGroup = "tabs";

    /// <summary>Registers the Go menu's commands.</summary>
    /// <param name="registry">The registry to add to.</param>
    public static void Register(IContributionRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        registry.AddCommand(new CommandDescriptor(
            ContributionIds.CommandPrefix + "nextTab",
            "Next tab",
            new CycleTabCommandHandler(1),
            DefaultGestures: ["Ctrl+Tab"],
            Menu: new MenuPlacement(MenuName, TabsGroup, 0)));

        registry.AddCommand(new CommandDescriptor(
            ContributionIds.CommandPrefix + "previousTab",
            "Previous tab",
            new CycleTabCommandHandler(-1),
            DefaultGestures: ["Ctrl+Shift+Tab"],
            Menu: new MenuPlacement(MenuName, TabsGroup, 1)));

        registry.AddCommand(new CommandDescriptor(
            ContributionIds.CommandPrefix + "closeTab",
            "Close tab",
            new CloseTabCommandHandler(),
            DefaultGestures: ["Ctrl+W"],
            Menu: new MenuPlacement(MenuName, TabsGroup, 2)));
    }
}
