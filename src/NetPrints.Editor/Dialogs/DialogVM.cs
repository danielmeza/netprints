using CommunityToolkit.Mvvm.ComponentModel;

namespace NetPrints.Editor.Dialogs;

/// <summary>
/// A dialog view model that can ask its host window to close with a result. <see
/// cref="NetPrints.Editor.Behaviors.DialogCloseBehavior"/> attaches to the window, watches <see
/// cref="CloseRequested"/> and closes the window with <see cref="Result"/> once it fires
/// (D16, ADR-0007 batch X2b).
/// </summary>
public interface IDialogCloseSource
{
    /// <summary>Raised once <see cref="Result"/> is set and the host window should close.</summary>
    event EventHandler? CloseRequested;

    /// <summary>The value the host window closes with.</summary>
    object? Result { get; }
}

/// <summary>
/// Base for a dialog view model whose accept/cancel commands close the host window with a result.
/// A concrete VM calls <see cref="RequestClose"/> from its own commands, since only it knows what
/// "accept" resolves to (SelectMethodDialogVM, SelectTypeDialogVM, TrustDialogVM).
/// </summary>
/// <typeparam name="TResult">Type of the value the dialog closes with.</typeparam>
public abstract class DialogVM<TResult> : ObservableObject, IDialogCloseSource
{
    /// <inheritdoc/>
    public event EventHandler? CloseRequested;

    /// <summary>The value to close the host window with, or <see langword="default"/> before then.</summary>
    public TResult? Result { get; private set; }

    object? IDialogCloseSource.Result => Result;

    /// <summary>Sets <see cref="Result"/> and raises <see cref="CloseRequested"/>.</summary>
    /// <param name="result">The value to close the host window with.</param>
    protected void RequestClose(TResult? result)
    {
        Result = result;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}
