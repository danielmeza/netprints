using Avalonia.Controls;
using NetPrints.Editor.Hosting.Avalonia;

namespace NetPrints.Editor.Dialogs;

/// <summary>The New project dialog.</summary>
public partial class NewProjectDialog : Window, IDialogResult<string>
{
    private readonly NewProjectDialogViewModel? viewModel;

    /// <summary>Loads the dialog's XAML, with no view model.</summary>
    public NewProjectDialog()
    {
        InitializeComponent();
    }

    /// <summary>Loads the dialog's XAML over a view model.</summary>
    /// <param name="viewModel">The dialog's view model.</param>
    public NewProjectDialog(NewProjectDialogViewModel viewModel)
    {
        this.viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }

    /// <summary>Gets the new project's path, or null until the project is created.</summary>
    public string? Result => viewModel?.Result;
}
