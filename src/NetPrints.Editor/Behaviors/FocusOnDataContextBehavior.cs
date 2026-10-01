using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.Xaml.Interactivity;

namespace NetPrints.Editor.Behaviors;

/// <summary>
/// Focuses the attached element each time its <c>DataContext</c> becomes a non-null object, once the pending layout
/// and visibility changes have been applied (D11 "no prebuilt fits, custom second": the prebuilt data-context
/// trigger fires before the element is visible, so the focus is lost). Used on the graph canvas so that opening a
/// graph makes its shortcuts work without a click.
/// </summary>
public sealed class FocusOnDataContextBehavior : StyledElementBehavior<InputElement>
{
    /// <inheritdoc/>
    protected override void OnAttached()
    {
        base.OnAttached();
        if (AssociatedObject is { } element)
        {
            element.DataContextChanged += OnDataContextChanged;
        }
    }

    /// <inheritdoc/>
    protected override void OnDetaching()
    {
        if (AssociatedObject is { } element)
        {
            element.DataContextChanged -= OnDataContextChanged;
        }

        base.OnDetaching();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (AssociatedObject is { DataContext: not null } element)
        {
            Dispatcher.UIThread.Post(() => element.Focus(), DispatcherPriority.Loaded);
        }
    }
}
