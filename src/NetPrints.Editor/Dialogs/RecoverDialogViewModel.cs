using CommunityToolkit.Mvvm.Input;
using NetPrints.Editor.Lifecycle;

namespace NetPrints.Editor.Dialogs;

/// <summary>Offers the backed-up files of the project being opened: restore them as unsaved changes, or discard them.</summary>
public sealed partial class RecoverDialogViewModel : DialogViewModel<RecoveryChoice>
{
    /// <summary>Builds the prompt.</summary>
    /// <param name="files">The backed-up files to list.</param>
    public RecoverDialogViewModel(IReadOnlyList<RecoveryFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        Files = files;
        DiscardIsDefault = files.Any(file => file.IsOlderThanFile);
    }

    /// <summary>The backed-up files, one row each.</summary>
    public IReadOnlyList<RecoveryFile> Files { get; }

    /// <summary>Gets whether Discard, not Restore, is the default button: a backup is older than its file, so restoring could lose newer work.</summary>
    public bool DiscardIsDefault { get; }

    /// <summary>Gets whether Restore is the default button.</summary>
    public bool RestoreIsDefault => !DiscardIsDefault;

    /// <summary>Closes the dialog asking to load the backups as unsaved changes.</summary>
    [RelayCommand]
    private void Restore() => RequestClose(RecoveryChoice.Restore);

    /// <summary>Closes the dialog asking to delete the backups.</summary>
    [RelayCommand]
    private void Discard() => RequestClose(RecoveryChoice.Discard);
}
