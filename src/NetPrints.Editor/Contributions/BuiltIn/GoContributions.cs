using NetPrints.Editor.Commands;
using NetPrints.Editor.Navigation;

namespace NetPrints.Editor.Contributions.BuiltIn;

/// <summary>The built-in commands of the Go menu and the connection context menu.</summary>
public static class GoContributions
{
    private const string MenuName = "Go";
    private const string TabsGroup = "tabs";
    private const string HistoryGroup = "history";
    private const string FindGroup = "find";
    private const string ConnectionGroup = "connection";

    /// <summary>Registers the Go menu's commands.</summary>
    /// <param name="registry">The registry to add to.</param>
    public static void Register(IEditorContributions registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        registry.AddCommand(new CommandDescriptor(
            ContributionIds.CommandPrefix + "goToAnything",
            "Go to anything…",
            new GoToAnythingCommandHandler(),
            DefaultGestures: [new(CommandModifiers.Ctrl, CommandKey.P)],
            Menu: new MenuPlacement(MenuName, FindGroup, 0)));

        registry.AddCommand(new CommandDescriptor(
            ContributionIds.CommandPrefix + "navigateBack",
            "Back",
            new NavigateHistoryCommandHandler(forward: false),
            DefaultGestures: [new(CommandModifiers.Alt, CommandKey.Left)],
            Menu: new MenuPlacement(MenuName, HistoryGroup, 0)));

        registry.AddCommand(new CommandDescriptor(
            ContributionIds.CommandPrefix + "navigateForward",
            "Forward",
            new NavigateHistoryCommandHandler(forward: true),
            DefaultGestures: [new(CommandModifiers.Alt, CommandKey.Right)],
            Menu: new MenuPlacement(MenuName, HistoryGroup, 1)));

        registry.AddCommand(new CommandDescriptor(
            ContributionIds.CommandPrefix + "nextTab",
            "Next tab",
            new CycleTabCommandHandler(1),
            DefaultGestures: [new(CommandModifiers.Ctrl, CommandKey.Tab)],
            Menu: new MenuPlacement(MenuName, TabsGroup, 0)));

        registry.AddCommand(new CommandDescriptor(
            ContributionIds.CommandPrefix + "previousTab",
            "Previous tab",
            new CycleTabCommandHandler(-1),
            DefaultGestures: [new(CommandModifiers.Ctrl | CommandModifiers.Shift, CommandKey.Tab)],
            Menu: new MenuPlacement(MenuName, TabsGroup, 1)));

        registry.AddCommand(new CommandDescriptor(
            ContributionIds.CommandPrefix + "closeTab",
            "Close tab",
            new CloseTabCommandHandler(),
            DefaultGestures: [new(CommandModifiers.Ctrl, CommandKey.W)],
            Menu: new MenuPlacement(MenuName, TabsGroup, 2)));

        AddConnectionCommand(registry, "goToSource", "Go to source", ConnectionEnd.Source, 0);
        AddConnectionCommand(registry, "goToTarget", "Go to target", ConnectionEnd.Target, 1);
    }

    private static void AddConnectionCommand(IEditorContributions registry, string name, string label, ConnectionEnd end, int order)
    {
        registry.AddCommand(new CommandDescriptor(
            ContributionIds.CommandPrefix + name,
            label,
            new GoToConnectionEndCommandHandler(end),
            Scope: CommandScope.Graph,
            Menu: new MenuPlacement(MenuName, ConnectionGroup, order)));

        registry.AddContextMenuItem(new ContextMenuItemDescriptor(
            ContributionIds.MenuPrefix + "connection." + name,
            ContextMenuTarget.Connection,
            ContributionIds.CommandPrefix + name,
            ConnectionGroup,
            order));
    }
}
