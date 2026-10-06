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
        Rows = [.. files.Select(file => new RecoveryRowViewModel(file))];
    }

    /// <summary>The backed-up files, one row each with its own restore-or-discard choice.</summary>
    public IReadOnlyList<RecoveryRowViewModel> Rows { get; }

    /// <summary>Gets what the user answered: the button, and with Restore the checked rows.</summary>
    public RecoveryAnswer Answer => new(Result, Result == RecoveryChoice.Restore ? [.. Rows.Where(row => row.Restore).Select(row => row.Path)] : []);

    /// <summary>Closes the dialog applying each row's choice: checked files load as unsaved changes, the others are discarded.</summary>
    [RelayCommand]
    private void Restore() => RequestClose(RecoveryChoice.Restore);

    /// <summary>Closes the dialog asking to delete every backup.</summary>
    [RelayCommand]
    private void Discard() => RequestClose(RecoveryChoice.Discard);
}
