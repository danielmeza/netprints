using Avalonia.Controls;
using NetPrints.Editor.Hosting.Avalonia;

namespace NetPrints.Editor.Dialogs;

/// <summary>Asks whether a project's extensions may be loaded (extension-points.md §8.3).</summary>
public partial class TrustDialog : Window, IDialogResult<bool>
{
    private readonly TrustDialogViewModel viewModel;

    /// <summary>Loads the dialog's XAML, with no project set.</summary>
    public TrustDialog() : this("", [])
    {
    }

    /// <summary>Loads the dialog's XAML for a project and its extension folders.</summary>
    /// <param name="projectPath">Full path of the project.</param>
    /// <param name="extensionFolders">Full paths of the project's extension folders.</param>
    public TrustDialog(string projectPath, IReadOnlyList<string> extensionFolders)
    {
        viewModel = new TrustDialogViewModel(projectPath, extensionFolders);
        DataContext = viewModel;
        InitializeComponent();
    }

    /// <summary>Whether the user chose to trust the project; <see langword="false"/> until then.</summary>
    public bool Result => viewModel.Result;
}
