using Avalonia.Controls;
using NetPrints.Editor.Hosting.Avalonia;

namespace NetPrints.Editor.Dialogs;

/// <summary>Asks the user to confirm an action that cannot be undone.</summary>
public partial class ConfirmDialog : Window, IDialogResult<bool>
{
    private readonly ConfirmDialogViewModel viewModel;

    /// <summary>Loads the dialog's XAML, with no message set.</summary>
    public ConfirmDialog() : this("", "", "OK")
    {
    }

    /// <summary>Loads the dialog's XAML for a question.</summary>
    /// <param name="title">Dialog window title.</param>
    /// <param name="message">What will happen.</param>
    /// <param name="confirmLabel">The text of the button that goes ahead.</param>
    public ConfirmDialog(string title, string message, string confirmLabel)
    {
        Title = title;
        viewModel = new ConfirmDialogViewModel(message, confirmLabel);
        DataContext = viewModel;
        InitializeComponent();
    }

    /// <summary>Whether the user confirmed; <see langword="false"/> until then.</summary>
    public bool Result => viewModel.Result;
}
