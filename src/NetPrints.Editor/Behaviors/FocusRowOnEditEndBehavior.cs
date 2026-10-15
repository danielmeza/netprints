using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactivity;

namespace NetPrints.Editor.Behaviors;

/// <summary>
/// Gives the keyboard back to the list row that holds an in-place edit box (D11 "no prebuilt fits, custom second"): after
/// Enter or Escape in the box, once the box has been hidden by the commit or cancel, the row's list item gets the focus, so
/// Delete and F2 work without a click. A refused name keeps the box visible, and the focus stays in it.
/// </summary>
internal sealed class FocusRowOnEditEndBehavior : StyledElementBehavior<TextBox>
{
    /// <inheritdoc/>
    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject?.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    /// <inheritdoc/>
    protected override void OnDetaching()
    {
        AssociatedObject?.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
        base.OnDetaching();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Escape)
        {
            Dispatcher.UIThread.Post(FocusRow, DispatcherPriority.Loaded);
        }
    }

    private void FocusRow()
    {
        if (AssociatedObject is { IsVisible: false } box)
        {
            box.FindAncestorOfType<ListBoxItem>()?.Focus();
        }
    }
}
