using Avalonia.Controls;
using NetPrints.Editor.ViewModels;

namespace NetPrints.Editor.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        // Closing the main window closes all class editor windows (PAR-14).
        (DataContext as MainEditorVM)?.OnMainWindowClosed();
    }
}
