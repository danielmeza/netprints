using Avalonia.Controls;
using NetPrints.Core;
using NetPrints.Editor.Hosting.Avalonia;

namespace NetPrints.Editor.Dialogs;

/// <summary>Chooses a method from a filterable list grouped by declaring type (FR-095).</summary>
public partial class SelectMethodDialog : Window, IDialogResult<MethodSpecifier>
{
    private readonly SelectMethodDialogViewModel viewModel;

    /// <summary>Loads the dialog's XAML, with no choices set.</summary>
    public SelectMethodDialog() : this([])
    {
    }

    /// <summary>Loads the dialog's XAML with the offered methods.</summary>
    /// <param name="methods">Methods offered by the chooser, nearest base type first.</param>
    /// <param name="overriddenNames">Names of the methods the class already has; their rows are dimmed and cannot be picked.</param>
    /// <param name="title">The window and shell title.</param>
    /// <param name="acceptLabel">The accept button's label.</param>
    public SelectMethodDialog(IEnumerable<MethodSpecifier> methods, IReadOnlySet<string>? overriddenNames = null,
        string title = SelectMethodDialogViewModel.OverrideTitle, string acceptLabel = SelectMethodDialogViewModel.OverrideLabel)
    {
        viewModel = new SelectMethodDialogViewModel(methods, overriddenNames, title, acceptLabel);
        DataContext = viewModel;
        InitializeComponent();
    }

    /// <summary>The dialog's result once closed, or <see langword="null"/> before then and after a cancel.</summary>
    public MethodSpecifier? Result => viewModel.Result;
}
