using NetPrints.Editor.Commands;
using NetPrints.Editor.Icons;

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
            IconId: IconIds.KeyboardShortcuts,
            Scope: CommandScope.Global,
            Menu: new MenuPlacement(MenuName, "help", 0)));

        registry.AddCommand(new CommandDescriptor(
            ContributionIds.CommandPrefix + "startPage",
            "Start page",
            new StartPageCommandHandler(),
            IconId: IconIds.Home,
            Scope: CommandScope.Global,
            Menu: new MenuPlacement(MenuName, "help", 1)));

        registry.AddCommand(new CommandDescriptor(
            ContributionIds.CommandPrefix + "about",
            "About NetPrints",
            new AboutCommandHandler(),
            IconId: IconIds.About,
            Scope: CommandScope.Global,
            Menu: new MenuPlacement(MenuName, "about", 0)));
    }
}
