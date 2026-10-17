using NetPrints.Editor.Contributions;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Commands.KeyboardShortcuts;

/// <summary>The keyboard shortcuts sheet: every registered command with its shortcuts, grouped as in the menu bar.</summary>
public sealed class KeyboardShortcutsViewModel
{
    /// <summary>Title of the group that holds the commands without a menu entry.</summary>
    public const string OtherTitle = "Other";

    /// <summary>Builds the sheet from the registry.</summary>
    /// <param name="registry">The registry whose commands are listed.</param>
    public KeyboardShortcutsViewModel(IContributionRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ContributionSnapshot snapshot = registry.Snapshot;
        CommandDescriptor[] commands = [.. snapshot.Commands];
        CommandDescriptor[] withMenu = [.. commands.Where(command => command.Menu is not null)];
        string[] menus = [.. MenuBarViewModel.StandardMenus, .. withMenu.Select(command => command.Menu?.Path ?? "").Distinct(StringComparer.Ordinal).Except(MenuBarViewModel.StandardMenus, StringComparer.Ordinal)];
        List<ShortcutGroup> groups = [.. menus.Select(menu => new ShortcutGroup(menu, MenuRows(snapshot, withMenu, menu)))];
        ShortcutRow[] others = [.. commands.Where(command => command.Menu is null).Select(command => Row(snapshot, command))];
        if (others.Length > 0)
        {
            groups.Add(new ShortcutGroup(OtherTitle, others));
        }

        Groups = [.. groups.Where(group => group.Rows.Count > 0)];
    }

    /// <summary>Gets the groups: the menus in menu bar order, then <see cref="OtherTitle"/> when a command has no menu.</summary>
    public IReadOnlyList<ShortcutGroup> Groups { get; }

    private static ShortcutRow[] MenuRows(ContributionSnapshot snapshot, CommandDescriptor[] withMenu, string menu)
    {
        CommandDescriptor[] inMenu = [.. withMenu.Where(command => command.Menu?.Path == menu)];
        string[] groups = [.. inMenu.Select(command => command.Menu?.Group ?? "").Distinct(StringComparer.Ordinal)];
        return [.. groups.SelectMany(group => inMenu.Where(command => command.Menu?.Group == group).OrderBy(command => command.Menu?.Order).Select(command => Row(snapshot, command)))];
    }

    private static ShortcutRow Row(ContributionSnapshot snapshot, CommandDescriptor command) =>
        new(command.Id, command.DisplayLabel, string.Join(", ", (command.DefaultGestures ?? []).Select(gesture => gesture.Display)), snapshot.OwnerNameOf(command.Id) ?? "");
}
