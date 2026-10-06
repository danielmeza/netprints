using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetPrints.Editor.Shell;
using NetPrints.Editor.State;

namespace NetPrints.Editor.StartPage;

/// <summary>The recent tile: the recent list with search, open, pin, unpin and remove.</summary>
internal sealed partial class RecentProjectsTileViewModel : ObservableObject
{
    private readonly RecentProjects? recent;
    private readonly IProjectActions actions;

    /// <summary>Creates the tile.</summary>
    /// <param name="recent">The recent list, or null when the editor keeps none.</param>
    /// <param name="actions">Opens the chosen project.</param>
    public RecentProjectsTileViewModel(RecentProjects? recent, IProjectActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        this.recent = recent;
        this.actions = actions;
        Refresh();
    }

    /// <summary>Gets the rows, pinned first.</summary>
    public ObservableCollection<RecentProjectItemViewModel> Items { get; } = [];

    /// <summary>Gets or sets the search text.</summary>
    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    /// <summary>Gets a value indicating whether no row shows.</summary>
    public bool IsEmpty => Items.Count == 0;

    /// <summary>Reads the list again, with the current search.</summary>
    public void Refresh()
    {
        Items.Clear();
        foreach (RecentProject entry in recent?.List(string.IsNullOrWhiteSpace(SearchText) ? null : SearchText) ?? [])
        {
            Items.Add(new RecentProjectItemViewModel(entry));
        }

        OnPropertyChanged(nameof(IsEmpty));
    }

    partial void OnSearchTextChanged(string value) => Refresh();

    [RelayCommand(CanExecute = nameof(CanOpen))]
    private async Task OpenAsync(RecentProjectItemViewModel item, CancellationToken cancellationToken)
    {
        if (await actions.ConfirmUnloadAsync(cancellationToken).ConfigureAwait(true))
        {
            await actions.OpenProjectAsync(item.Path, cancellationToken).ConfigureAwait(true);
        }
    }

    private static bool CanOpen(RecentProjectItemViewModel? item) => item is { IsAvailable: true };

    [RelayCommand]
    private void Pin(RecentProjectItemViewModel item) => Change(() => recent?.Pin(item.Path));

    [RelayCommand]
    private void Unpin(RecentProjectItemViewModel item) => Change(() => recent?.Unpin(item.Path));

    [RelayCommand]
    private void Remove(RecentProjectItemViewModel item) => Change(() => recent?.Remove(item.Path));

    private void Change(Action change)
    {
        change();
        Refresh();
    }
}
