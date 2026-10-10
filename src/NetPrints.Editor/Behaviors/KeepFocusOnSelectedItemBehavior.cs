using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.Xaml.Interactivity;

namespace NetPrints.Editor.Behaviors;

/// <summary>
/// Keeps the keyboard on a list whose rows are rebuilt (D11 "no prebuilt fits, custom second"): while the user works the
/// list with keys, each time its selection changes to a row, that row's container gets the focus once layout has built
/// it. Without it a command that rebuilds the rows (pin, remove) drops the focus and the next key goes nowhere. A pointer
/// press ends the keyboard use.
/// </summary>
internal sealed class KeepFocusOnSelectedItemBehavior : StyledElementBehavior<ListBox>
{
    private bool keyboard;

    /// <inheritdoc/>
    protected override void OnAttached()
    {
        base.OnAttached();
        if (AssociatedObject is { } list)
        {
            list.AddHandler(InputElement.KeyDownEvent, OnKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
            list.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
            list.SelectionChanged += OnSelectionChanged;
        }
    }

    /// <inheritdoc/>
    protected override void OnDetaching()
    {
        if (AssociatedObject is { } list)
        {
            list.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
            list.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressed);
            list.SelectionChanged -= OnSelectionChanged;
        }

        base.OnDetaching();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e) => keyboard = true;

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e) => keyboard = false;

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (keyboard && AssociatedObject is { SelectedItem: not null })
        {
            Dispatcher.UIThread.Post(FocusSelected, DispatcherPriority.Loaded);
        }
    }

    private void FocusSelected()
    {
        if (AssociatedObject is { SelectedItem: { } item } list && list.ContainerFromItem(item) is { } container)
        {
            container.Focus();
        }
    }
}
