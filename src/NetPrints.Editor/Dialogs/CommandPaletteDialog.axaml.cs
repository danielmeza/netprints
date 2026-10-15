using Avalonia.Controls;
using NetPrints.Editor.Commands.CommandPalette;

namespace NetPrints.Editor.Dialogs;

/// <summary>The command palette window (Ctrl+Shift+P): a search box over the list of commands.</summary>
public partial class CommandPaletteDialog : Window
{
    /// <summary>Loads the dialog's XAML, listing nothing.</summary>
    public CommandPaletteDialog()
    {
        InitializeComponent();
    }

    /// <summary>Loads the dialog's XAML for a palette.</summary>
    /// <param name="palette">The commands and the filter.</param>
    public CommandPaletteDialog(CommandPaletteViewModel palette)
    {
        DataContext = palette;
        InitializeComponent();
    }
}
