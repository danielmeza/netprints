using Avalonia.Controls;
using Avalonia.Interactivity;
using NetPrints.Core;
using NetPrints.Editor.Hosting.Avalonia;

namespace NetPrints.Editor.Dialogs;

/// <summary>Chooses a type; defaults to <c>object</c> (PAR-58).</summary>
public partial class SelectTypeDialog : Window, IDialogResult<TypeSpecifier>
{
    private readonly List<TypeSpecifier> types = [];

    /// <summary>Loads the dialog's XAML, with no choices set.</summary>
    public SelectTypeDialog()
    {
        InitializeComponent();
    }

    /// <summary>Loads the dialog's XAML with the offered types and an initial selection.</summary>
    /// <param name="types">Types offered by the chooser.</param>
    /// <param name="initial">Initially selected type.</param>
    public SelectTypeDialog(IEnumerable<TypeSpecifier> types, TypeSpecifier initial) : this()
    {
        this.types = types.ToList();
        TypeBox.ItemsSource = this.types;
        TypeBox.SelectedItem = initial;
        TypeBox.Text = initial.ToString();
    }

    /// <summary>The dialog's result once closed via <see cref="OnSelectClicked"/>, or <see langword="null"/> before then.</summary>
    public TypeSpecifier? Result { get; private set; }

    /// <summary>Resolves the selected item or the typed text to a type.</summary>
    public TypeSpecifier? ResolveSelection()
    {
        if (TypeBox.SelectedItem is TypeSpecifier selected)
        {
            return selected;
        }

        string text = TypeBox.Text?.Trim() ?? "";
        return types.FirstOrDefault(t => string.Equals(t.ToString(), text, StringComparison.Ordinal))
            ?? types.FirstOrDefault(t => string.Equals(t.Name, text, StringComparison.Ordinal))
            ?? types.FirstOrDefault(t => string.Equals(t.ToString(), text, StringComparison.OrdinalIgnoreCase));
    }

    private void OnSelectClicked(object? sender, RoutedEventArgs e)
    {
        Result = ResolveSelection();
        if (Result is not null)
        {
            Close(Result);
        }
    }
}
