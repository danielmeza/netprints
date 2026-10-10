using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Shell;
using NetPrints.Editor.State;

namespace NetPrints.Editor.StartPage;

/// <summary>The recent tile: the recent list in date groups, with search, open, pin, unpin, remove, copy path, open containing folder and the keyboard commands.</summary>
internal sealed partial class RecentProjectsTileViewModel : ObservableObject
{
    private readonly RecentProjects? recent;
    private readonly IProjectActions actions;
    private readonly TimeProvider time;
    private readonly IClipboardService? clipboard;
    private readonly IFolderLauncher? folders;
    private readonly Dictionary<string, bool> availability = new(StringComparer.Ordinal);

    /// <summary>Creates the tile.</summary>
    /// <param name="recent">The recent list, or null when the editor keeps none.</param>
    /// <param name="actions">Opens the chosen project.</param>
    /// <param name="time">The clock of the relative dates and the date groups; null for the system clock.</param>
    /// <param name="clipboard">Receives a copied path; null makes Copy path do nothing.</param>
    /// <param name="folders">Shows a project's folder; null makes Open containing folder do nothing.</param>
    public RecentProjectsTileViewModel(RecentProjects? recent, IProjectActions actions, TimeProvider? time = null, IClipboardService? clipboard = null, IFolderLauncher? folders = null)
    {
        ArgumentNullException.ThrowIfNull(actions);
        this.recent = recent;
        this.actions = actions;
        this.time = time ?? TimeProvider.System;
        this.clipboard = clipboard;
        this.folders = folders;
        Refresh();
    }

    /// <summary>Gets the rows, pinned first.</summary>
    public ObservableCollection<RecentProjectItemViewModel> Items { get; } = [];

    /// <summary>Gets or sets the search text.</summary>
    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    /// <summary>Gets or sets the row the keyboard commands act on.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenSelectedCommand))]
    [NotifyCanExecuteChangedFor(nameof(RemoveSelectedCommand))]
    [NotifyCanExecuteChangedFor(nameof(TogglePinSelectedCommand))]
    public partial RecentProjectItemViewModel? SelectedItem { get; set; }

    /// <summary>Gets a value indicating whether no row shows.</summary>
    public bool IsEmpty => Items.Count == 0;

    /// <summary>Gets the check of the latest <see cref="Refresh"/>: completed once the rows it could not answer from the cache have their availability.</summary>
    public Task AvailabilityChecked { get; private set; } = Task.CompletedTask;

    /// <summary>Reads the list again, with the current search. Rows show at once; their availability follows from a check off the UI thread.</summary>
    public void Refresh()
    {
        string? selected = SelectedItem?.Path;
        Items.Clear();
        DateTimeOffset now = time.GetUtcNow();
        string? previous = null;
        List<RecentProjectItemViewModel> unknown = [];
        foreach (RecentProject listed in (recent?.ListUnchecked(string.IsNullOrWhiteSpace(SearchText) ? null : SearchText) ?? []).OrderByDescending(entry => entry.Pinned).ThenByDescending(entry => entry.LastOpenedUtc))
        {
            bool known = availability.TryGetValue(listed.Path, out bool cached);
            RecentProject entry = listed with { IsAvailable = !known || cached };
            DateTimeOffset opened = new(DateTime.SpecifyKind(entry.LastOpenedUtc, DateTimeKind.Utc));
            string group = RecentTime.GroupOf(entry.Pinned, opened, now, time.LocalTimeZone);
            var item = new RecentProjectItemViewModel(entry, this, group, group != previous, RecentTime.Describe(opened, now, time.LocalTimeZone, CultureInfo.CurrentCulture));
            Items.Add(item);
            if (!known)
            {
                unknown.Add(item);
            }

            previous = group;
        }

        AvailabilityChecked = unknown.Count == 0 || recent is null ? Task.CompletedTask : CheckAvailabilityAsync(recent, unknown, CancellationToken.None);

        SelectedItem = selected is null ? null : Items.FirstOrDefault(item => item.Path == selected);
        OnPropertyChanged(nameof(IsEmpty));
    }

    private async Task CheckAvailabilityAsync(RecentProjects projects, List<RecentProjectItemViewModel> rows, CancellationToken cancellationToken)
    {
        string[] paths = [.. rows.Select(row => row.Path)];
        bool[] found = await Task.Run(() => paths.Select(projects.IsAvailable).ToArray(), cancellationToken).ConfigureAwait(true);
        for (int i = 0; i < rows.Count; i++)
        {
            availability[paths[i]] = found[i];
            rows[i].IsAvailable = found[i];
        }

        OpenCommand.NotifyCanExecuteChanged();
        OpenSelectedCommand.NotifyCanExecuteChanged();
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

    [RelayCommand]
    private void TogglePin(RecentProjectItemViewModel item) => Change(() =>
    {
        if (item.Pinned)
        {
            recent?.Unpin(item.Path);
        }
        else
        {
            recent?.Pin(item.Path);
        }
    });

    [RelayCommand]
    private async Task CopyPathAsync(RecentProjectItemViewModel item)
    {
        if (clipboard is not null)
        {
            await clipboard.SetTextAsync(item.Path).ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private void OpenContainingFolder(RecentProjectItemViewModel item)
    {
        if (System.IO.Path.GetDirectoryName(item.Path) is { Length: > 0 } folder)
        {
            folders?.Reveal(folder);
        }
    }

    [RelayCommand]
    private void SelectFirst() => SelectedItem = Items.FirstOrDefault();

    [RelayCommand(CanExecute = nameof(CanOpenSelected))]
    private Task OpenSelectedAsync(CancellationToken cancellationToken) => SelectedItem is { } item ? OpenAsync(item, cancellationToken) : Task.CompletedTask;

    private bool CanOpenSelected() => SelectedItem is { IsAvailable: true };

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void RemoveSelected()
    {
        if (SelectedItem is not { } item)
        {
            return;
        }

        int index = Items.IndexOf(item);
        Change(() => recent?.Remove(item.Path));
        SelectedItem = Items.Count == 0 ? null : Items[Math.Min(index, Items.Count - 1)];
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void TogglePinSelected()
    {
        if (SelectedItem is { } item)
        {
            TogglePin(item);
        }
    }

    private bool HasSelection() => SelectedItem is not null;

    private void Change(Action change)
    {
        change();
        Refresh();
    }
}
