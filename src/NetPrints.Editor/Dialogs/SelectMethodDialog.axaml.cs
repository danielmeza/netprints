using Avalonia.Controls;
using NetPrints.Core;
using NetPrints.Editor.Hosting.Avalonia;

namespace NetPrints.Editor.Dialogs;

/// <summary>Chooses a method; the first one is preselected (PAR-59).</summary>
public partial class SelectMethodDialog : Window, IDialogResult<MethodSpecifier>
{
    private readonly SelectMethodDialogVM viewModel;

    /// <summary>Loads the dialog's XAML, with no choices set.</summary>
    public SelectMethodDialog() : this([])
    {
    }

    /// <summary>Loads the dialog's XAML with the offered methods; the first one is preselected.</summary>
    /// <param name="methods">Methods offered by the chooser.</param>
    public SelectMethodDialog(IEnumerable<MethodSpecifier> methods)
    {
        viewModel = new SelectMethodDialogVM(methods);
        DataContext = viewModel;
        InitializeComponent();
    }

    /// <summary>The dialog's result once closed via the VM's <c>SelectCommand</c>, or <see langword="null"/> before then.</summary>
    public MethodSpecifier? Result => viewModel.Result;

    /// <summary>The currently selected method, or <see langword="null"/> if none is selected.</summary>
    public MethodSpecifier? SelectedMethod => viewModel.SelectedMethod;
}
