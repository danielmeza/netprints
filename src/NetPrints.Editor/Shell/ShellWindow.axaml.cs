using Avalonia.Controls;

namespace NetPrints.Editor.Shell;

/// <summary>The one editor window: the menu bar, the command bar, the docked layout and the status bar, all generated from the registry and bound to a <see cref="ShellViewModel"/>.</summary>
public partial class ShellWindow : Window
{
    /// <summary>Loads the window's XAML.</summary>
    public ShellWindow()
    {
        InitializeComponent();
    }
}
