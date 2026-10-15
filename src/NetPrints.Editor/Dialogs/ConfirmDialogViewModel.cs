using CommunityToolkit.Mvvm.Input;

namespace NetPrints.Editor.Dialogs;

/// <summary>Asks the user to confirm an action that cannot be undone.</summary>
public sealed partial class ConfirmDialogViewModel : DialogViewModel<bool>
{
    /// <summary>Builds the prompt.</summary>
    /// <param name="message">What will happen.</param>
    /// <param name="confirmLabel">The text of the button that goes ahead.</param>
    public ConfirmDialogViewModel(string message, string confirmLabel)
    {
        Message = message;
        ConfirmLabel = confirmLabel;
    }

    /// <summary>The text shown above the buttons.</summary>
    public string Message { get; }

    /// <summary>The text of the confirm button.</summary>
    public string ConfirmLabel { get; }

    /// <summary>Closes the dialog with the action confirmed.</summary>
    [RelayCommand]
    private void Confirm() => RequestClose(true);

    /// <summary>Closes the dialog with the action declined.</summary>
    [RelayCommand]
    private void Cancel() => RequestClose(false);
}
