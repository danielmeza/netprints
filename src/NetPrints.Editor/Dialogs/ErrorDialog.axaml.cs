using Avalonia.Controls;

namespace NetPrints.Editor.Dialogs;

/// <summary>Shows an error message with copy-friendly text (PAR-04, PAR-13, PAR-17, PAR-18).</summary>
public partial class ErrorDialog : Window
{
    private readonly ErrorDialogVM viewModel;

    /// <summary>Loads the dialog's XAML, with no title or message set.</summary>
    public ErrorDialog() : this("", "")
    {
    }

    /// <summary>Loads the dialog's XAML with a title and message.</summary>
    /// <param name="title">Dialog window title.</param>
    /// <param name="message">Copy-friendly error message.</param>
    public ErrorDialog(string title, string message)
    {
        Title = title;
        viewModel = new ErrorDialogVM(message);
        DataContext = viewModel;
        InitializeComponent();
    }

    /// <summary>The displayed message text.</summary>
    public string Message => viewModel.Message;
}
