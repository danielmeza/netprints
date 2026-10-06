using NetPrints.Editor.Commands;

namespace NetPrints.Editor.Contributions.BuiltIn;

/// <summary>The built-in commands of the Help menu.</summary>
public static class HelpContributions
{
    private const string MenuName = "Help";

    /// <summary>Registers the Help menu's commands.</summary>
    /// <param name="registry">The registry to add to.</param>
    public static void Register(IContributionRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        registry.AddCommand(new CommandDescriptor(
            ContributionIds.CommandPrefix + "keyboardShortcuts",
            "Keyboard shortcuts",
            new KeyboardShortcutsCommandHandler(),
            IconKind: "Keyboard",
            Scope: CommandScope.Global,
            Menu: new MenuPlacement(MenuName, "help", 0)));

        registry.AddCommand(new CommandDescriptor(
            ContributionIds.CommandPrefix + "startPage",
            "Start page",
            new StartPageCommandHandler(),
            IconKind: "Home",
            Scope: CommandScope.Global,
            Menu: new MenuPlacement(MenuName, "help", 1)));

        registry.AddCommand(new CommandDescriptor(
            ContributionIds.CommandPrefix + "about",
            "About NetPrints",
            new AboutCommandHandler(),
            IconKind: "InformationOutline",
            Scope: CommandScope.Global,
            Menu: new MenuPlacement(MenuName, "about", 0)));
    }
}
