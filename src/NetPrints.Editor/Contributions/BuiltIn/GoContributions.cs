using NetPrints.Editor.Commands;

namespace NetPrints.Editor.Contributions.BuiltIn;

/// <summary>The built-in commands of the Go menu; go-to, the palette and the connection commands arrive with T075 to T077.</summary>
public static class GoContributions
{
    private const string MenuName = "Go";
    private const string TabsGroup = "tabs";
    private const string HistoryGroup = "history";
    private const string FindGroup = "find";

    /// <summary>Registers the Go menu's commands.</summary>
    /// <param name="registry">The registry to add to.</param>
    public static void Register(IContributionRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        registry.AddCommand(new CommandDescriptor(
            ContributionIds.CommandPrefix + "goToAnything",
            "Go to anything…",
            new GoToAnythingCommandHandler(),
            DefaultGestures: ["Ctrl+P"],
            Menu: new MenuPlacement(MenuName, FindGroup, 0)));

        registry.AddCommand(new CommandDescriptor(
            ContributionIds.CommandPrefix + "navigateBack",
            "Back",
            new NavigateHistoryCommandHandler(forward: false),
            DefaultGestures: ["Alt+Left"],
            Menu: new MenuPlacement(MenuName, HistoryGroup, 0)));

        registry.AddCommand(new CommandDescriptor(
            ContributionIds.CommandPrefix + "navigateForward",
            "Forward",
            new NavigateHistoryCommandHandler(forward: true),
            DefaultGestures: ["Alt+Right"],
            Menu: new MenuPlacement(MenuName, HistoryGroup, 1)));

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
