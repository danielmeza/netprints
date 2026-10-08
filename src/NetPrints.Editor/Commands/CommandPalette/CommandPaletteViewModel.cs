using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Dialogs;
using NetPrints.Editor.Navigation;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Commands.CommandPalette;

/// <summary>
/// The command palette (FR-060): every registered command, including disabled ones, filtered by the typed text with the
/// research R9 ranking. Enter runs the selected command when it is enabled and closes the palette; Esc only closes it.
/// </summary>
public sealed partial class CommandPaletteViewModel : DialogViewModel<object>
{
    private readonly PaletteItem[] all;
    private readonly CommandInvoker invoker;

    /// <summary>Lists the commands of a registry as they are enabled now.</summary>
    /// <param name="registry">The registry whose commands are listed.</param>
    /// <param name="invoker">Tells whether a command can run and runs it.</param>
    public CommandPaletteViewModel(IContributionRegistry registry, CommandInvoker invoker)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(invoker);
        this.invoker = invoker;
        all =
        [
            .. registry.Commands.Select(command => new PaletteItem(
                command,
                command.Label,
                string.Join(", ", command.DefaultGestures ?? []),
                command.Menu?.Path ?? "",
                invoker.CanRun(command))),
        ];
        Refresh();
    }

    /// <summary>Gets the commands that match <see cref="Query"/>, best first.</summary>
    public ObservableCollection<PaletteItem> Items { get; } = [];

    /// <summary>Gets or sets the text typed.</summary>
    [ObservableProperty]
    public partial string Query { get; set; } = "";

    /// <summary>Gets or sets the highlighted command, or null when nothing matches.</summary>
    [ObservableProperty]
    public partial PaletteItem? SelectedItem { get; set; }

    /// <summary>Gets a value indicating whether nothing matches <see cref="Query"/>.</summary>
    public bool IsEmpty => Items.Count == 0;

    /// <summary>Runs the highlighted command and closes the palette; does nothing when it is disabled.</summary>
    [RelayCommand]
    private void RunSelected()
    {
        if (SelectedItem is not { IsEnabled: true } item)
        {
            return;
        }

        RequestClose(null);
        invoker.TryRun(item.Command);
    }

    /// <summary>Highlights the next command, stopping at the last.</summary>
    [RelayCommand]
    private void SelectNext() => Move(1);

    /// <summary>Highlights the previous command, stopping at the first.</summary>
    [RelayCommand]
    private void SelectPrevious() => Move(-1);

    /// <summary>Closes the palette without running anything.</summary>
    [RelayCommand]
    private void Close() => RequestClose(null);

    partial void OnQueryChanged(string value) => Refresh();

    private void Move(int step)
    {
        if (Items.Count == 0)
        {
            return;
        }

        int index = SelectedItem is null ? -1 : Items.IndexOf(SelectedItem);
        SelectedItem = Items[Math.Clamp(index + step, 0, Items.Count - 1)];
    }

    private void Refresh()
    {
        Items.Clear();
        foreach (PaletteItem item in MatchRanking.Order(all, item => item.Label, Query.Trim()))
        {
            Items.Add(item);
        }

        SelectedItem = Items.Count > 0 ? Items[0] : null;
        OnPropertyChanged(nameof(IsEmpty));
    }
}
