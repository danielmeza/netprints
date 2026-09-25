using Avalonia.Controls;
using Avalonia.Interactivity;

namespace NetPrints.Editor.Dialogs;

/// <summary>Shows an error message with copy-friendly text (PAR-04, PAR-13, PAR-17, PAR-18).</summary>
public partial class ErrorDialog : Window
{
    /// <summary>Loads the dialog's XAML, with no title or message set.</summary>
    public ErrorDialog()
    {
        InitializeComponent();
    }

    /// <summary>Loads the dialog's XAML with a title and message.</summary>
    /// <param name="title">Dialog window title.</param>
    /// <param name="message">Copy-friendly error message.</param>
    public ErrorDialog(string title, string message) : this()
    {
        Title = title;
        MessageBox.Text = message;
    }

    /// <summary>The displayed message text.</summary>
    public string Message => MessageBox.Text ?? "";

    private void OnOkClicked(object? sender, RoutedEventArgs e) => Close();
}
