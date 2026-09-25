using Avalonia.Controls;

namespace NetPrints.Editor.Main;

/// <summary>The editor's main window: project lifecycle, settings, references and the class list.</summary>
public partial class MainWindow : Window
{
    /// <summary>Loads the window's XAML.</summary>
    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>Closes every class editor window (PAR-14), via the view model's <see cref="MainEditorVM.OnMainWindowClosed"/>.</summary>
    /// <param name="e">Unused; forwarded to the base implementation.</param>
    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        // Closing the main window closes all class editor windows (PAR-14).
        (DataContext as MainEditorVM)?.OnMainWindowClosed();
    }
}
