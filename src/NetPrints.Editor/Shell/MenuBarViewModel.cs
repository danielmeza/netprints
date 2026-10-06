using NetPrints.Editor.Contributions;

namespace NetPrints.Editor.Shell;

/// <summary>The menu bar generated from the registry: the menus in order, their groups separated by dividers, each entry re-queried on every command-state pulse.</summary>
public sealed class MenuBarViewModel : IDisposable
{
    internal static readonly string[] StandardMenus = ["File", "Edit", "View", "Go", "Build", "Help"];

    private readonly CommandInvoker invoker;
    private readonly List<CommandEntryViewModel> entries = [];

    /// <summary>Creates the menus.</summary>
    /// <param name="registry">The registry.</param>
    /// <param name="invoker">Runs and queries the commands.</param>
    public MenuBarViewModel(IContributionRegistry registry, CommandInvoker invoker)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(invoker);
        this.invoker = invoker;
        CommandDescriptor[] withMenu = [.. registry.Commands.Where(command => command.Menu is not null)];
        string[] names = [.. StandardMenus, .. withMenu.Select(command => command.Menu?.Path ?? "").Distinct(StringComparer.Ordinal).Except(StandardMenus, StringComparer.Ordinal)];
        Menus = [.. names.Select(name => Build(name, withMenu))];
        invoker.CommandStatesChanged += OnCommandStatesChanged;
    }

    /// <summary>Gets the menus: File, Edit, View, Go, Build and Help, then any other in registration order.</summary>
    public IReadOnlyList<MenuViewModel> Menus { get; }

    /// <summary>Stops following the command states.</summary>
    public void Dispose() => invoker.CommandStatesChanged -= OnCommandStatesChanged;

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

            foreach (CommandDescriptor command in commands.Where(command => command.Menu?.Group == group).OrderBy(command => command.Menu?.Order))
            {
                var entry = new CommandEntryViewModel(command, invoker, AutomationIds.MenuPrefix);
                entries.Add(entry);
                items.Add(entry);
            }
        }

        return new MenuViewModel(name, items);
    }

    private void OnCommandStatesChanged(object? sender, EventArgs e) => entries.ForEach(entry => entry.Refresh());
}
