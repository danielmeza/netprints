using Avalonia.Controls;
using NetPrints.Editor.Hosting.Avalonia;
using NetPrints.Editor.Lifecycle;

namespace NetPrints.Editor.Dialogs;

/// <summary>Asks what to do with the unsaved files before the project is unloaded.</summary>
public partial class UnsavedChangesDialog : Window, IDialogResult<UnloadChoice>
{
    private readonly UnsavedChangesDialogViewModel viewModel;

    /// <summary>Loads the dialog's XAML, listing no files.</summary>
    public UnsavedChangesDialog() : this([])
    {
    }

    /// <summary>Loads the dialog's XAML for the given files.</summary>
    /// <param name="files">The unsaved files to list.</param>
    public UnsavedChangesDialog(IReadOnlyList<UnsavedFile> files)
    {
        viewModel = new UnsavedChangesDialogViewModel(files);
        DataContext = viewModel;
        InitializeComponent();
    }

    /// <summary>What the user chose; <see cref="UnloadChoice.Cancel"/> until then.</summary>
    public UnloadChoice Result => viewModel.Result;
}
