using Avalonia.Controls;

namespace NetPrints.Editor.Dialogs;

/// <summary>Shows the editor version and the project links (Help › About NetPrints).</summary>
public partial class AboutDialog : Window
{
    /// <summary>Loads the dialog's XAML, with no version or links.</summary>
    public AboutDialog()
    {
        InitializeComponent();
    }

    /// <summary>Loads the dialog's XAML for the given content.</summary>
    /// <param name="about">The version and the project links.</param>
    public AboutDialog(AboutViewModel about)
    {
        DataContext = about;
        InitializeComponent();
    }
}
