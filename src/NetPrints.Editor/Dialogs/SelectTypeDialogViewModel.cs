using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetPrints.Core;

namespace NetPrints.Editor.Dialogs;

/// <summary>Chooses a type; defaults to <c>object</c> (PAR-58, batch X2b).</summary>
public sealed partial class SelectTypeDialogViewModel : DialogViewModel<TypeSpecifier>
{
    private readonly List<TypeSpecifier> types;

    /// <summary>Offers <paramref name="types"/>, starting from <paramref name="initial"/>.</summary>
    /// <param name="types">Types offered by the chooser.</param>
    /// <param name="initial">Initially selected type.</param>
    public SelectTypeDialogViewModel(IEnumerable<TypeSpecifier> types, TypeSpecifier initial)
    {
        this.types = [.. types];
        SelectedType = initial;
        TypedText = initial.ToString();
    }

    /// <summary>Types offered by the chooser.</summary>
    public IReadOnlyList<TypeSpecifier> Types => types;

    /// <summary>The item chosen from the drop-down, or <see langword="null"/> if the user only typed.</summary>
    [ObservableProperty]
    public partial TypeSpecifier? SelectedType { get; set; }

    /// <summary>The editable box's current text (the chooser is editable, PAR-58).</summary>
    [ObservableProperty]
    public partial string TypedText { get; set; } = "";

    /// <summary>
    /// Resolves the selected item or the typed text to a type (moved from
    /// <c>SelectTypeDialog.ResolveSelection</c>, D16).
    /// </summary>
    public TypeSpecifier? ResolveSelection()
    {
        if (SelectedType is { } selected)
        {
            return selected;
        }

        string text = TypedText.Trim();
        return types.FirstOrDefault(t => string.Equals(t.ToString(), text, StringComparison.Ordinal))
            ?? types.FirstOrDefault(t => string.Equals(t.Name, text, StringComparison.Ordinal))
            ?? types.FirstOrDefault(t => string.Equals(t.ToString(), text, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Closes the dialog with the resolved type.</summary>
    [RelayCommand]
    private void Select() => RequestClose(ResolveSelection());
}
