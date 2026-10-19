using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.State;
using NetPrints.Extensibility.Settings;

namespace NetPrints.Editor.Settings;

/// <summary>An override of a command that is not loaded, kept in the file and listed under "Not loaded".</summary>
/// <param name="summary">The command id with the gestures the entry adds and removes.</param>
/// <param name="remove">Deletes the entry.</param>
internal sealed partial class NotLoadedRowViewModel(string summary, Func<Task> remove) : ObservableObject
{
    /// <summary>Gets the command id with the gestures the entry adds and removes.</summary>
    public string Summary { get; } = summary;

    [RelayCommand]
    private Task RemoveAsync() => remove();
}

/// <summary>The question that names the command already using a gesture; accepting moves the gesture, cancelling changes nothing.</summary>
internal sealed partial class GestureConflictViewModel(string message, Func<Task> accept, Action cancel) : ObservableObject
{
    /// <summary>Gets the question, naming the other command first.</summary>
    public string Message { get; } = message;

    [RelayCommand]
    private Task AcceptAsync() => accept();

    [RelayCommand]
    private void Cancel() => cancel();
}

/// <summary>
/// The Keyboard shortcuts page (FR-045 to FR-047): one row per registered command with label, owner, scope, default and
/// current gestures, a filter by text or by a recorded gesture, add, remove and reset, conflict resolution and the
/// overrides of commands that are not loaded.
/// </summary>
internal sealed partial class KeyboardShortcutsItemViewModel : SettingsItemViewModel, IDisposable
{
    private readonly IContributionRegistry registry;
    private readonly KeyBindingEditor editor;
    private string pendingGesture = "";
    private ShortcutEditRowViewModel? pendingRow;

    /// <summary>Builds the page over a registry and the store that keeps the <c>keyboard</c> section.</summary>
    /// <param name="registry">The registry whose commands are listed and whose bindings the page changes.</param>
    /// <param name="store">The settings store.</param>
    public KeyboardShortcutsItemViewModel(IContributionRegistry registry, ISettingsStore store)
        : base(Strings.Settings_Keyboard_Label, Strings.Settings_Keyboard_Description)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(store);
        this.registry = registry;
        editor = new KeyBindingEditor(registry, store);
        registry.Changed += OnRegistryChanged;
        Refresh();
    }

    /// <summary>Gets every command's row.</summary>
    public ObservableCollection<ShortcutEditRowViewModel> Rows { get; } = [];

    /// <summary>Gets the rows that pass the filter.</summary>
    public ObservableCollection<ShortcutEditRowViewModel> VisibleRows { get; } = [];

    /// <summary>Gets the overrides of commands that are not loaded.</summary>
    public ObservableCollection<NotLoadedRowViewModel> NotLoaded { get; } = [];

    /// <summary>Gets the gestures of the file that were reported and ignored, one text each.</summary>
    public ObservableCollection<string> IssueTexts { get; } = [];

    /// <summary>Gets or sets the text rows are filtered by: label, id or owner.</summary>
    [ObservableProperty]
    public partial string FilterText { get; set; } = "";

    /// <summary>Gets the gesture rows are filtered by; empty for none.</summary>
    [ObservableProperty]
    public partial string FilterGesture { get; private set; } = "";

    /// <summary>Gets a value indicating whether the next key chord becomes the gesture filter.</summary>
    [ObservableProperty]
    public partial bool IsRecordingFilter { get; private set; }

    /// <summary>Gets the row the next key chord is added to, or <see langword="null"/>.</summary>
    [ObservableProperty]
    public partial ShortcutEditRowViewModel? RecordingRow { get; private set; }

    /// <summary>Gets a number that changes each time the keyboard should go back to the filter box.</summary>
    [ObservableProperty]
    public partial int FilterFocusRequest { get; private set; }

    /// <summary>Gets a value indicating whether the next key chord is recorded.</summary>
    public bool IsRecording => IsRecordingFilter || RecordingRow is not null;

    /// <summary>Gets the question shown when a recorded gesture is used by another command; <see langword="null"/> when there is none.</summary>
    [ObservableProperty]
    public partial GestureConflictViewModel? Conflict { get; private set; }

    /// <summary>Gets the reason a recorded gesture was refused; empty when none was.</summary>
    [ObservableProperty]
    public partial string Refusal { get; private set; } = "";

    /// <summary>Gets a value indicating whether <see cref="Refusal"/> has a message.</summary>
    public bool HasRefusal => Refusal.Length > 0;

    /// <summary>Gets a value indicating whether some override names a command that is not loaded.</summary>
    public bool HasNotLoaded => NotLoaded.Count > 0;

    /// <summary>Gets a value indicating whether the file has gestures that were ignored.</summary>
    public bool HasIssues => IssueTexts.Count > 0;

    /// <inheritdoc/>
    public void Dispose()
    {
        registry.Changed -= OnRegistryChanged;
        registry.IsRecordingGestures = false;
    }

    /// <summary>Applies a key chord the page recorded: it is added to the row being recorded, or becomes the gesture filter.</summary>
    /// <param name="gestureText">The chord in canonical form.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the chord is applied.</returns>
    [RelayCommand]
    public async Task CaptureAsync(string gestureText, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(gestureText);
        if (IsRecordingFilter)
        {
            IsRecordingFilter = false;
            FilterGesture = gestureText;
            ApplyFilter();
            RequestFilterFocus();
            return;
        }

        if (RecordingRow is not { } row)
        {
            return;
        }

        RecordingRow = null;
        row.IsRecording = false;
        Conflict = null;
        Refusal = "";
        CommandDescriptor? command = registry.Snapshot.Commands.FirstOrDefault(c => c.Id == row.CommandId);
        if (command is null)
        {
            return;
        }

        GestureAddResult result = await editor.AddAsync(command, gestureText, acceptConflict: false, cancellationToken);
        if (result.Refusal is { } reason)
        {
            Refusal = reason;
        }
        else if (result.Conflict is { } other)
        {
            pendingGesture = gestureText;
            pendingRow = row;
            string gesture = CommandGesture.TryParse(gestureText, out CommandGesture? parsed) ? parsed.Display : gestureText;
            Conflict = new GestureConflictViewModel(
                string.Format(CultureInfo.CurrentCulture, Strings.Settings_Keyboard_Conflict, gesture, other.DisplayLabel, row.Label),
                AcceptConflictAsync,
                CancelConflict);
        }

        if (Conflict is null)
        {
            RequestFilterFocus();
        }
    }

    /// <summary>Stops recording without a gesture.</summary>
    [RelayCommand]
    public void CancelRecording()
    {
        IsRecordingFilter = false;
        if (RecordingRow is { } row)
        {
            row.IsRecording = false;
        }

        RecordingRow = null;
    }

    partial void OnFilterTextChanged(string value) => ApplyFilter();

    partial void OnIsRecordingFilterChanged(bool value) => RecordingChanged();

    partial void OnRecordingRowChanged(ShortcutEditRowViewModel? value) => RecordingChanged();

    private void RecordingChanged()
    {
        registry.IsRecordingGestures = IsRecording;
        OnPropertyChanged(nameof(IsRecording));
    }

    partial void OnRefusalChanged(string value) => OnPropertyChanged(nameof(HasRefusal));

    [RelayCommand]
    private void RecordFilter()
    {
        CancelRecording();
        IsRecordingFilter = true;
    }

    /// <summary>Records the next key chord for the first row the filter shows (Enter in the filter box).</summary>
    [RelayCommand]
    private void RecordFirstVisible()
    {
        if (VisibleRows.Count == 0)
        {
            return;
        }

        ShortcutEditRowViewModel row = VisibleRows[0];
        CancelRecording();
        Conflict = null;
        Refusal = "";
        row.IsRecording = true;
        RecordingRow = row;
    }

    [RelayCommand]
    private void ClearFilterGesture()
    {
        FilterGesture = "";
        ApplyFilter();
    }

    [RelayCommand]
    private async Task ResetAllAsync(CancellationToken cancellationToken)
    {
        await editor.ResetAllAsync(cancellationToken);
        RequestFilterFocus();
    }

    private async Task AcceptConflictAsync()
    {
        ShortcutEditRowViewModel? row = pendingRow;
        Conflict = null;
        CommandDescriptor? command = registry.Snapshot.Commands.FirstOrDefault(c => c.Id == row?.CommandId);
        if (command is not null)
        {
            await editor.AddAsync(command, pendingGesture, acceptConflict: true, CancellationToken.None);
        }

        RequestFilterFocus();
    }

    private void CancelConflict()
    {
        Conflict = null;
        RequestFilterFocus();
    }

    private void RequestFilterFocus() => FilterFocusRequest++;

    private void ToggleRecording(ShortcutEditRowViewModel row)
    {
        bool wasRecording = row.IsRecording;
        CancelRecording();
        Conflict = null;
        Refusal = "";
        if (!wasRecording)
        {
            row.IsRecording = true;
            RecordingRow = row;
        }
    }

    private void OnRegistryChanged(object? sender, ContributionSnapshot snapshot) => Refresh();

    private void Refresh()
    {
        ContributionSnapshot snapshot = registry.Snapshot;
        EffectiveKeyBindings bindings = snapshot.KeyBindings;
        string? recordingId = RecordingRow?.CommandId;
        Rows.Clear();
        foreach (CommandDescriptor command in snapshot.Commands)
        {
            CommandBindings own = bindings.For(command.Id);
            GestureChipViewModel[] chips = [.. own.Effective.Select(gesture => new GestureChipViewModel(gesture.Display, async () =>
            {
                await editor.RemoveAsync(command, gesture, CancellationToken.None);
                RequestFilterFocus();
            }))];
            Rows.Add(new ShortcutEditRowViewModel(
                command.Id,
                command.DisplayLabel,
                snapshot.OwnerNameOf(command.Id) ?? Strings.Settings_Keyboard_BuiltInOwner,
                command.Scope.ToString(),
                string.Join(", ", own.Defaults.Select(gesture => gesture.Display)),
                chips,
                own.IsChanged,
                own.Overridden.Count > 0,
                ToggleRecording,
                async id =>
                {
                    await editor.ResetAsync(id, CancellationToken.None);
                    RequestFilterFocus();
                }));
        }

        RecordingRow = Rows.FirstOrDefault(row => row.CommandId == recordingId);
        if (RecordingRow is { } recording)
        {
            recording.IsRecording = true;
        }

        NotLoaded.Clear();
        foreach (KeyBindingOverride entry in bindings.NotLoaded)
        {
            NotLoaded.Add(new NotLoadedRowViewModel(
                entry.Problem ?? string.Format(CultureInfo.CurrentCulture, Strings.Settings_Keyboard_NotLoadedSummary, entry.Command, string.Join(", ", entry.Add), string.Join(", ", entry.Remove)),
                () => editor.RemoveEntryAsync(entry, CancellationToken.None)));
        }

        IssueTexts.Clear();
        foreach (KeyBindingIssue issue in bindings.Issues)
        {
            IssueTexts.Add(string.Format(CultureInfo.CurrentCulture, Strings.Settings_Keyboard_IssueFormat, issue.Command, issue.Gesture, issue.Reason));
        }

        OnPropertyChanged(nameof(HasNotLoaded));
        OnPropertyChanged(nameof(HasIssues));
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        bool byGesture = FilterGesture.Length > 0 && CommandGesture.TryParse(FilterGesture, out _);
        CommandGesture.TryParse(FilterGesture, out CommandGesture? wanted);
        VisibleRows.Clear();
        foreach (ShortcutEditRowViewModel row in Rows.Where(row => MatchesText(row) && (!byGesture || HasGesture(row, wanted))))
        {
            VisibleRows.Add(row);
        }
    }

    private bool MatchesText(ShortcutEditRowViewModel row) =>
        FilterText.Length == 0
        || row.Label.Contains(FilterText, StringComparison.CurrentCultureIgnoreCase)
        || row.CommandId.Contains(FilterText, StringComparison.CurrentCultureIgnoreCase)
        || row.Owner.Contains(FilterText, StringComparison.CurrentCultureIgnoreCase);

    private bool HasGesture(ShortcutEditRowViewModel row, CommandGesture? wanted) =>
        wanted is not null && registry.Snapshot.KeyBindings.TryGet(row.CommandId, out CommandBindings bindings) && bindings.Effective.Contains(wanted);
}
