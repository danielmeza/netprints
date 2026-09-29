namespace NetPrints.Editor.Dialogs;

/// <summary>View model for <see cref="ErrorDialog"/> (D3/D16, ADR-0007 batch F15; the dialog itself has
/// no result, so this is not a <see cref="DialogVM{TResult}"/>).</summary>
public sealed class ErrorDialogVM
{
    /// <summary>Holds the message to show.</summary>
    /// <param name="message">Copy-friendly error message.</param>
    public ErrorDialogVM(string message)
    {
        Message = message;
    }

    /// <summary>Copy-friendly error message.</summary>
    public string Message { get; }
}
