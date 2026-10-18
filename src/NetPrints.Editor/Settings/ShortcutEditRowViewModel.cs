using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NetPrints.Editor.Settings;

/// <summary>One gesture of a command on the Keyboard shortcuts page, with the command that removes it.</summary>
/// <param name="text">The gesture as displayed.</param>
/// <param name="remove">Removes the gesture from the command.</param>
internal sealed partial class GestureChipViewModel(string text, Func<Task> remove) : ObservableObject
{
    /// <summary>Gets the gesture as displayed.</summary>
    public string Text { get; } = text;

    [RelayCommand]
    private Task RemoveAsync() => remove();
}

/// <summary>One command on the Keyboard shortcuts page: its label, owner, scope, default and current gestures.</summary>
internal sealed partial class ShortcutEditRowViewModel : ObservableObject
{
    private readonly Action<ShortcutEditRowViewModel> toggleRecording;
    private readonly Func<string, Task> reset;

    /// <summary>Creates a row.</summary>
    /// <param name="commandId">The id of the command.</param>
    /// <param name="label">The label of the command.</param>
    /// <param name="owner">The owner shown beside the label.</param>
    /// <param name="scope">The scope of the command as text.</param>
    /// <param name="defaults">The default gestures as displayed.</param>
    /// <param name="gestures">The gestures in force.</param>
    /// <param name="isChanged">Whether the gestures differ from the defaults.</param>
    /// <param name="isOverridden">Whether a user binding of another command took a default of this one.</param>
    /// <param name="toggleRecording">Starts or ends recording a gesture for the row.</param>
    /// <param name="reset">Resets the command by its id.</param>
    public ShortcutEditRowViewModel(
        string commandId,
        string label,
        string owner,
        string scope,
        string defaults,
        IReadOnlyList<GestureChipViewModel> gestures,
        bool isChanged,
        bool isOverridden,
        Action<ShortcutEditRowViewModel> toggleRecording,
        Func<string, Task> reset)
    {
        CommandId = commandId;
        Label = label;
        Owner = owner;
        Scope = scope;
        Defaults = defaults;
        Gestures = new ObservableCollection<GestureChipViewModel>(gestures);
        IsChanged = isChanged;
        IsOverridden = isOverridden;
        this.toggleRecording = toggleRecording;
        this.reset = reset;
    }

    /// <summary>Gets the id of the command.</summary>
    public string CommandId { get; }

    /// <summary>Gets the label of the command.</summary>
    public string Label { get; }

    /// <summary>Gets the owner: the extension's name, or the built-in marker.</summary>
    public string Owner { get; }

    /// <summary>Gets the scope of the command as text.</summary>
    public string Scope { get; }

    /// <summary>Gets the default gestures as displayed, or empty.</summary>
    public string Defaults { get; }

    /// <summary>Gets the gestures in force, each with its remove command.</summary>
    public ObservableCollection<GestureChipViewModel> Gestures { get; }

    /// <summary>Gets the gestures in force as one text.</summary>
    public string Current => string.Join(", ", Gestures.Select(gesture => gesture.Text));

    /// <summary>Gets a value indicating whether the gestures differ from the defaults.</summary>
    public bool IsChanged { get; }

    /// <summary>Gets a value indicating whether a user binding of another command took a default of this command.</summary>
    public bool IsOverridden { get; }

    /// <summary>Gets or sets a value indicating whether the next key chord is recorded for this command.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AddLabel))]
    public partial bool IsRecording { get; set; }

    /// <summary>Gets the text of the add button: it asks for a key chord while one is recorded.</summary>
    public string AddLabel => IsRecording ? Strings.Settings_Keyboard_Recording : Strings.Settings_Keyboard_AddGesture;

    [RelayCommand]
    private void ToggleRecording() => toggleRecording(this);

    [RelayCommand(CanExecute = nameof(IsChanged))]
    private Task ResetAsync() => reset(CommandId);
}
