using Avalonia.Controls;
using Avalonia.Interactivity;

namespace NetPrints.Editor.Views.Dialogs;

/// <summary>Shows an error message with copy-friendly text (PAR-04, PAR-13, PAR-17, PAR-18).</summary>
public partial class ErrorDialog : Window
{
    public ErrorDialog()
    {
        InitializeComponent();
    }

    public ErrorDialog(string title, string message) : this()
    {
        Title = title;
        MessageBox.Text = message;
    }

    public string Message => MessageBox.Text ?? "";

    private void OnOkClicked(object? sender, RoutedEventArgs e) => Close();
}
