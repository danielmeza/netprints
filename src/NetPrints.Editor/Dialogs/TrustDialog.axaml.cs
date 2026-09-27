using Avalonia.Controls;
using Avalonia.Interactivity;
using NetPrints.Editor.Hosting.Avalonia;

namespace NetPrints.Editor.Dialogs;

/// <summary>Asks whether a project's extensions may be loaded (extension-points.md §8.3).</summary>
public partial class TrustDialog : Window, IDialogResult<bool>
{
    /// <summary>Loads the dialog's XAML, with no project set.</summary>
    public TrustDialog()
    {
        InitializeComponent();
    }

    /// <summary>Loads the dialog's XAML for a project and its extension folders.</summary>
    /// <param name="projectPath">Full path of the project.</param>
    /// <param name="extensionFolders">Full paths of the project's extension folders.</param>
    public TrustDialog(string projectPath, IReadOnlyList<string> extensionFolders) : this()
    {
        Prompt.Text = $"'{projectPath}' wants to load extensions. They run code in the editor and can change what it does. " +
            "Trust this project only if you trust its author.";
        FolderList.ItemsSource = extensionFolders;
    }

    /// <summary>Whether the user chose to trust the project; <see langword="false"/> until then.</summary>
    public bool Result { get; private set; }

    private void OnTrustClicked(object? sender, RoutedEventArgs e)
    {
        Result = true;
        Close(true);
    }

    private void OnDontLoadClicked(object? sender, RoutedEventArgs e) => Close(false);
}
