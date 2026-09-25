using Avalonia.Controls;
using Avalonia.Interactivity;
using NetPrints.Core;
using NetPrints.Editor.Hosting.Avalonia;

namespace NetPrints.Editor.Dialogs;

/// <summary>Chooses a type; defaults to <c>object</c> (PAR-58).</summary>
public partial class SelectTypeDialog : Window, IDialogResult<TypeSpecifier>
{
    private readonly List<TypeSpecifier> types = [];

    public SelectTypeDialog()
    {
        InitializeComponent();
    }

    public SelectTypeDialog(IEnumerable<TypeSpecifier> types, TypeSpecifier initial) : this()
    {
        this.types = types.ToList();
        TypeBox.ItemsSource = this.types;
        TypeBox.SelectedItem = initial;
        TypeBox.Text = initial.ToString();
    }

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
