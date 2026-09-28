using Avalonia.Controls;
using Avalonia.Xaml.Interactivity;
using NetPrints.Editor.Dialogs;

namespace NetPrints.Editor.Behaviors;

/// <summary>
/// Closes the attached window with its view model's result once the view model raises <see
/// cref="IDialogCloseSource.CloseRequested"/> (D11 "no prebuilt fits, custom second"; D16, ADR-0007
/// batch X2b). The one reusable close-with-result behavior shared by every dialog whose VM derives
/// from <see cref="DialogVM{TResult}"/> (<c>SelectMethodDialog</c>, <c>SelectTypeDialog</c>,
/// <c>TrustDialog</c>); a dialog that closes with no result keeps using the prebuilt
/// <c>ButtonClickEventTriggerBehavior</c> + <c>CloseWindowAction</c> pair instead.
/// </summary>
public sealed class DialogCloseBehavior : StyledElementBehavior<Window>
{
    private IDialogCloseSource? source;

    /// <inheritdoc/>
    protected override void OnAttached()
    {
        base.OnAttached();
        Hook(DataContext);
    }

    /// <inheritdoc/>
    protected override void OnDetaching()
    {
        Hook(null);
        base.OnDetaching();
    }

    /// <inheritdoc/>
    protected override void OnDataContextChangedEvent()
    {
        base.OnDataContextChangedEvent();
        Hook(DataContext);
    }

    private void Hook(object? context)
    {
        if (source is not null)
        {
            source.CloseRequested -= OnCloseRequested;
        }

        source = context as IDialogCloseSource;

        if (source is not null)
        {
            source.CloseRequested += OnCloseRequested;
        }
    }

    private void OnCloseRequested(object? sender, EventArgs e)
    {
        if (AssociatedObject is { } window && sender is IDialogCloseSource closed)
        {
            window.Close(closed.Result);
        }
    }
}
