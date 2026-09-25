using Avalonia.Controls;
using Avalonia.Interactivity;

namespace NetPrints.Editor.Views;

/// <summary>References dialog (PAR-16..21).</summary>
public partial class ReferencesDialog : Window
{
    public ReferencesDialog()
    {
        InitializeComponent();
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e) => Close();
}
