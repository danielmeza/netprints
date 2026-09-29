using Avalonia.Controls;
using NetPrints.Core;
using NetPrints.Editor.Hosting.Avalonia;

namespace NetPrints.Editor.Dialogs;

/// <summary>Chooses a type; defaults to <c>object</c> (PAR-58).</summary>
public partial class SelectTypeDialog : Window, IDialogResult<TypeSpecifier>
{
    private readonly SelectTypeDialogVM viewModel;

    /// <summary>Loads the dialog's XAML, with no choices set.</summary>
    public SelectTypeDialog() : this([], TypeSpecifier.FromType<object>())
    {
    }

    /// <summary>Loads the dialog's XAML with the offered types and an initial selection.</summary>
    /// <param name="types">Types offered by the chooser.</param>
    /// <param name="initial">Initially selected type.</param>
    public SelectTypeDialog(IEnumerable<TypeSpecifier> types, TypeSpecifier initial)
    {
        viewModel = new SelectTypeDialogVM(types, initial);
        DataContext = viewModel;
        InitializeComponent();
    }

    /// <summary>The dialog's result once closed via the VM's <c>SelectCommand</c>, or <see langword="null"/> before then.</summary>
    public TypeSpecifier? Result => viewModel.Result;

    /// <summary>Resolves the selected item or the typed text to a type (forwards to the VM, D16).</summary>
    public TypeSpecifier? ResolveSelection() => viewModel.ResolveSelection();
}
