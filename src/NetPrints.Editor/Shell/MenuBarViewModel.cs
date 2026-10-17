using System.Collections.ObjectModel;
using NetPrints.Editor.Commands;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Contributions.BuiltIn;

namespace NetPrints.Editor.Shell;

/// <summary>The menu bar generated from the registry: the menus in order, their groups separated by dividers, each entry re-queried on every command-state pulse.</summary>
public sealed class MenuBarViewModel : IDisposable
{
    internal static readonly string[] StandardMenus = ["File", "Edit", "View", "Go", "Build", "Help"];

    private readonly IContributionRegistry registry;
    private readonly CommandInvoker invoker;
    private readonly List<CommandEntryViewModel> entries = [];

    /// <summary>Creates the menus.</summary>
    /// <param name="registry">The registry; the menus are rebuilt from its snapshot whenever it changes.</param>
    /// <param name="invoker">Runs and queries the commands.</param>
    public MenuBarViewModel(IContributionRegistry registry, CommandInvoker invoker)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(invoker);
        this.registry = registry;
        this.invoker = invoker;
        Rebuild(registry.Snapshot);
        invoker.CommandStatesChanged += OnCommandStatesChanged;
        registry.Changed += OnRegistryChanged;
    }

    /// <summary>Gets the menus: File, Edit, View, Go, Build and Help, then any other in registration order.</summary>
    public ObservableCollection<MenuViewModel> Menus { get; } = [];

    /// <summary>Stops following the command states and the registry.</summary>
    public void Dispose()
    {
        invoker.CommandStatesChanged -= OnCommandStatesChanged;
        registry.Changed -= OnRegistryChanged;
    }

    private void OnRegistryChanged(object? sender, ContributionSnapshot snapshot) => Rebuild(snapshot);

    private void Rebuild(IContributionSnapshot snapshot)
    {
        entries.Clear();
        CommandDescriptor[] withMenu = [.. snapshot.Commands.Where(command => command.Menu is not null)];
        string[] names = [.. StandardMenus, .. withMenu.Select(command => command.Menu?.Path ?? "").Distinct(StringComparer.Ordinal).Except(StandardMenus, StringComparer.Ordinal)];
        MenuViewModel[] built = [.. names.Select(name => Build(name, withMenu))];
        Menus.Clear();
        foreach (MenuViewModel menu in built)
        {
            Menus.Add(menu);
        }
    }

    private MenuViewModel Build(string name, CommandDescriptor[] withMenu)
    {
        CommandDescriptor[] commands = [.. withMenu.Where(command => command.Menu?.Path == name)];
        string[] groups = [.. commands.Select(command => command.Menu?.Group ?? "").Distinct(StringComparer.Ordinal)];
        List<CommandEntryViewModel> items = [];
        foreach (string group in groups)
        {
            if (items.Count > 0)
            {
                items.Add(CommandEntryViewModel.Separator);
            }

            List<string> openedSubmenus = [];
            foreach (CommandDescriptor command in commands.Where(command => command.Menu?.Group == group).OrderBy(command => command.Menu?.Order))
            {
                if (command.Menu?.Submenu is not { } submenu)
                {
                    items.Add(Entry(command));
                    continue;
                }

                if (openedSubmenus.Contains(submenu))
                {
                    continue;
                }

                openedSubmenus.Add(submenu);
                CommandEntryViewModel[] children = [.. commands.Where(other => other.Menu?.Group == group && other.Menu.Submenu == submenu).OrderBy(other => other.Menu?.Order).Select(Entry)];
                items.Add(CommandEntryViewModel.CreateSubmenu(submenu, AutomationIds.MenuPrefix + name + "." + submenu, children));
            }
        }

        return new MenuViewModel(name, items);
    }

    private CommandEntryViewModel Entry(CommandDescriptor command)
    {
        var entry = new CommandEntryViewModel(command, invoker, AutomationIds.MenuPrefix);
        entries.Add(entry);
        return entry;
    }

    private void OnCommandStatesChanged(object? sender, EventArgs e) => entries.ForEach(entry => entry.Refresh());
}
