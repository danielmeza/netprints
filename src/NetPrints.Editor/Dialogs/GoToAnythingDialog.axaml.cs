using Avalonia.Controls;
using NetPrints.Editor.Navigation;

namespace NetPrints.Editor.Dialogs;

/// <summary>The go to anything window (Ctrl+P): a search box over grouped results.</summary>
public partial class GoToAnythingDialog : Window
{
    /// <summary>Loads the dialog's XAML, listing nothing.</summary>
    public GoToAnythingDialog()
    {
        InitializeComponent();
    }

    /// <summary>Loads the dialog's XAML for a search.</summary>
    /// <param name="goTo">The providers and the filter.</param>
    public GoToAnythingDialog(GoToAnythingViewModel goTo)
    {
        DataContext = goTo;
        InitializeComponent();
    }
}
