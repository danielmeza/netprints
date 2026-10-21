using System.Collections.ObjectModel;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Contributions.BuiltIn;

namespace NetPrints.Editor.Shell;

/// <summary>The command bar generated from the registry: one slot per command bar position, re-queried on every command-state pulse.</summary>
public sealed class CommandBarViewModel : IDisposable
{
    private readonly IContributionRegistry registry;
    private readonly CommandInvoker invoker;
    private readonly Func<int> compileErrors;
    private CommandEntryViewModel? compile;

    /// <summary>Creates the slots.</summary>
    /// <param name="registry">The registry; the slots are rebuilt from its snapshot whenever it changes.</param>
    /// <param name="invoker">Runs and queries the commands.</param>
    /// <param name="compileErrors">Gets how many errors the last compile found.</param>
    public CommandBarViewModel(IContributionRegistry registry, CommandInvoker invoker, Func<int> compileErrors)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(invoker);
        ArgumentNullException.ThrowIfNull(compileErrors);
        this.registry = registry;
        this.invoker = invoker;
        this.compileErrors = compileErrors;
        Rebuild(registry.Snapshot);
        invoker.CommandStatesChanged += OnCommandStatesChanged;
        registry.Changed += OnRegistryChanged;
    }

    /// <summary>Gets the slots in order.</summary>
    public ObservableCollection<CommandBarSlotViewModel> Slots { get; } = [];

    /// <summary>Re-reads the error count shown on the compile button.</summary>
    public void RefreshBadge()
    {
        if (compile is not null)
        {
            compile.BadgeCount = compileErrors();
        }
    }

    /// <summary>Stops following the command states and the registry.</summary>
    public void Dispose()
    {
        invoker.CommandStatesChanged -= OnCommandStatesChanged;
        registry.Changed -= OnRegistryChanged;
    }

    private void OnRegistryChanged(object? sender, ContributionSnapshot snapshot) => Rebuild(snapshot);

    private void Rebuild(IContributionSnapshot snapshot)
    {
        CommandBarSlotViewModel[] built =
        [.. snapshot.Commands
            .Where(command => command.CommandBarOrder is not null)
            .GroupBy(command => command.CommandBarOrder)
            .OrderBy(group => group.Key)
            .Select(group => new CommandBarSlotViewModel([.. group.Select(command => new CommandEntryViewModel(command, invoker, AutomationIds.CommandBarPrefix))]))];
        foreach (CommandBarSlotViewModel slot in built)
        {
            slot.Refresh();
        }

        Slots.Clear();
        foreach (CommandBarSlotViewModel slot in built)
        {
            Slots.Add(slot);
        }

        compile = built.SelectMany(slot => slot.Items).FirstOrDefault(entry => entry.Id == BuildContributions.CompileId);
        RefreshBadge();
    }

    private void OnCommandStatesChanged(object? sender, EventArgs e)
    {
        CommandContext pulse = invoker.CreateContext();
        foreach (CommandBarSlotViewModel slot in Slots)
        {
            slot.Refresh(pulse);
        }

        RefreshBadge();
    }
}
