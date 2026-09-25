using Avalonia.Controls;
using Avalonia.Interactivity;

namespace NetPrints.Editor.References;

/// <summary>References dialog (PAR-16..21).</summary>
public partial class ReferencesDialog : Window
{
    /// <summary>Loads the dialog's XAML.</summary>
    public ReferencesDialog()
    {
        InitializeComponent();
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e) => Close();
}
