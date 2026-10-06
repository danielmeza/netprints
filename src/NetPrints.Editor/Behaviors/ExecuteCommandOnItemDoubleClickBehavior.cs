using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactivity;

namespace NetPrints.Editor.Behaviors;

/// <summary>
/// Runs <see cref="Command"/> with the row's item on the second press of a double click (<c>ClickCount == 2</c>) anywhere
/// on a row of the attached items control: the whole item container, its indentation, padding and empty space, not only the
/// template's content. Avalonia raises <c>DoubleTapped</c> only when both presses have the same source element, so a row
/// that is re-templated or realized again between the presses (selection, scrolling, a slow UI thread) loses it; the click
/// count depends on time and position only. Presses outside every row, other buttons and other counts do nothing.
/// </summary>
public sealed class ExecuteCommandOnItemDoubleClickBehavior : StyledElementBehavior<ItemsControl>
{
    private const int DoubleClickCount = 2;

    /// <summary>Identifies <see cref="Command"/>.</summary>
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<ExecuteCommandOnItemDoubleClickBehavior, ICommand?>(nameof(Command));

    /// <summary>Gets or sets the command run with the double-clicked row's <see cref="StyledElement.DataContext"/> as its parameter.</summary>
    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    /// <inheritdoc/>
    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject?.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    /// <inheritdoc/>
    protected override void OnDetaching()
    {
        AssociatedObject?.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressed);
        base.OnDetaching();
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.ClickCount != DoubleClickCount || !e.GetCurrentPoint(AssociatedObject).Properties.IsLeftButtonPressed
            || Command is not { } command || e.Source is not Visual source)
        {
            return;
        }

        foreach (Visual visual in source.GetSelfAndVisualAncestors())
        {
            if (visual is Control { DataContext: { } item } container && ItemsControl.ItemsControlFromItemContainer(container) is not null)
            {
                if (command.CanExecute(item))
                {
                    command.Execute(item);
                }

                return;
            }
        }
    }
}
