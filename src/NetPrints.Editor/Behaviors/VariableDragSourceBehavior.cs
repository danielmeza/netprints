using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Xaml.Interactivity;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Variables;

namespace NetPrints.Editor.Behaviors;

/// <summary>
/// Makes a variable row's name a drag source for the graph canvas (PAR-57, US5): pressing the left button on the control
/// whose data context is a <see cref="MemberVariableViewModel"/> or a <see cref="LocalVariableViewModel"/> and moving the
/// pointer a few pixels starts the drag. The handlers run in the tunnel phase and see handled events, so a
/// <see cref="TextBox"/> that marks the press handled still starts the drag.
/// </summary>
public sealed class VariableDragSourceBehavior : StyledElementBehavior<Control>
{
    private readonly DragSourceHelper dragSource = new();

    /// <inheritdoc/>
    protected override void OnAttached()
    {
        base.OnAttached();
        if (AssociatedObject is { } element)
        {
            element.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
            element.AddHandler(InputElement.PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
            element.AddHandler(InputElement.PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
        }
    }

    /// <inheritdoc/>
    protected override void OnDetaching()
    {
        if (AssociatedObject is { } element)
        {
            element.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressed);
            element.RemoveHandler(InputElement.PointerMovedEvent, OnPointerMoved);
            element.RemoveHandler(InputElement.PointerReleasedEvent, OnPointerReleased);
        }

        base.OnDetaching();
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (AssociatedObject is { DataContext: MemberVariableViewModel or LocalVariableViewModel } element)
        {
            dragSource.Pressed(e, element, element.DataContext);
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (AssociatedObject is { } element)
        {
            dragSource.Moved(e, element);
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e) => dragSource.Released();
}
