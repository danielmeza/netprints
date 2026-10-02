using NetPrints.Editor.Contributions;
using NetPrints.Editor.Contributions.BuiltIn;

namespace NetPrints.Editor.Shell;

/// <summary>The command bar generated from the registry: one slot per command bar position, re-queried on every command-state pulse.</summary>
public sealed class CommandBarViewModel : IDisposable
{
    private readonly CommandInvoker invoker;
    private readonly Func<int> compileErrors;
    private readonly CommandEntryViewModel? compile;

    /// <summary>Creates the slots.</summary>
    /// <param name="registry">The registry.</param>
    /// <param name="invoker">Runs and queries the commands.</param>
    /// <param name="compileErrors">Gets how many errors the last compile found.</param>
    public CommandBarViewModel(IContributionRegistry registry, CommandInvoker invoker, Func<int> compileErrors)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(invoker);
        ArgumentNullException.ThrowIfNull(compileErrors);
        this.invoker = invoker;
        this.compileErrors = compileErrors;
        Slots =
        [.. registry.Commands
            .Where(command => command.CommandBarOrder is not null)
            .GroupBy(command => command.CommandBarOrder)
            .OrderBy(group => group.Key)
            .Select(group => new CommandBarSlotViewModel([.. group.Select(command => new CommandEntryViewModel(command, invoker, AutomationIds.CommandBarPrefix))]))];
        foreach (CommandBarSlotViewModel slot in Slots)
        {
            slot.Refresh();
        }

        compile = Slots.SelectMany(slot => slot.Items).FirstOrDefault(entry => entry.Id == BuildContributions.CompileId);
        RefreshBadge();
        invoker.CommandStatesChanged += OnCommandStatesChanged;
    }

    /// <summary>Gets the slots in order.</summary>
    public IReadOnlyList<CommandBarSlotViewModel> Slots { get; }

    /// <summary>Re-reads the error count shown on the compile button.</summary>
    public void RefreshBadge()
    {
        if (compile is not null)
        {
            compile.BadgeCount = compileErrors();
        }
    }

    /// <summary>Stops following the command states.</summary>
    public void Dispose() => invoker.CommandStatesChanged -= OnCommandStatesChanged;

    private void OnCommandStatesChanged(object? sender, EventArgs e)
    {
        foreach (CommandBarSlotViewModel slot in Slots)
        {
            slot.Refresh();
        }

        RefreshBadge();
    }
}
