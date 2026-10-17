using Avalonia.Controls;

namespace NetPrints.Editor.Dialogs;

/// <summary>Shows the view model of a dialog an extension registered, with its view found by data template.</summary>
public partial class ExtensionDialog : Window
{
    /// <summary>Loads the dialog's XAML, with no view model.</summary>
    public ExtensionDialog()
    {
        InitializeComponent();
    }

    /// <summary>Loads the dialog's XAML for a view model.</summary>
    /// <param name="title">The window title.</param>
    /// <param name="viewModel">The view model to show.</param>
    public ExtensionDialog(string title, object viewModel)
    {
        Title = title;
        DataContext = viewModel;
        InitializeComponent();
    }
}
