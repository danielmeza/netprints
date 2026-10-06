using CommunityToolkit.Mvvm.Input;
using NetPrints.Editor.Lifecycle;

namespace NetPrints.Editor.Dialogs;

/// <summary>Asks what to do with the unsaved files before the project is unloaded: save them all, drop the changes, or keep the project open.</summary>
public sealed partial class UnsavedChangesDialogViewModel : DialogViewModel<UnloadChoice>
{
    /// <summary>Builds the prompt.</summary>
    /// <param name="files">The unsaved files to list.</param>
    public UnsavedChangesDialogViewModel(IReadOnlyList<UnsavedFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        Files = files;
    }

    /// <summary>The unsaved files, one row each.</summary>
    public IReadOnlyList<UnsavedFile> Files { get; }

    /// <summary>Closes the dialog asking to save every unsaved file first.</summary>
    [RelayCommand]
    private void SaveAll() => RequestClose(UnloadChoice.Save);

    /// <summary>Closes the dialog asking to unload without saving.</summary>
    [RelayCommand]
    private void DontSave() => RequestClose(UnloadChoice.Discard);

    /// <summary>Closes the dialog keeping the project open.</summary>
    [RelayCommand]
    private void Cancel() => RequestClose(UnloadChoice.Cancel);
}
