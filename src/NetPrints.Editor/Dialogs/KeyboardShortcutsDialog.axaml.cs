using Avalonia.Controls;
using NetPrints.Editor.Commands.KeyboardShortcuts;

namespace NetPrints.Editor.Dialogs;

/// <summary>Lists every registered command with its shortcuts (Help › Keyboard shortcuts).</summary>
public partial class KeyboardShortcutsDialog : Window
{
    /// <summary>Loads the dialog's XAML, listing nothing.</summary>
    public KeyboardShortcutsDialog()
    {
        InitializeComponent();
    }

    /// <summary>Loads the dialog's XAML for a sheet.</summary>
    /// <param name="sheet">The commands and their shortcuts.</param>
    public KeyboardShortcutsDialog(KeyboardShortcutsViewModel sheet)
    {
        DataContext = sheet;
        InitializeComponent();
    }
}
