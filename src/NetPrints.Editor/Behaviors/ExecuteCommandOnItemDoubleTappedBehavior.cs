using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactivity;

namespace NetPrints.Editor.Behaviors;

/// <summary>
/// Runs <see cref="Command"/> with the row's item when a row of the attached items control is double-tapped anywhere
/// (the whole item container: its indentation, padding and empty space, not only the template's content). The press
/// that selects the row and the double tap are recognized on the container, so a layout change inside the template
/// between the two presses cannot split them. Double taps outside every row do nothing.
/// </summary>
public sealed class ExecuteCommandOnItemDoubleTappedBehavior : StyledElementBehavior<ItemsControl>
{
    /// <summary>Identifies <see cref="Command"/>.</summary>
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<ExecuteCommandOnItemDoubleTappedBehavior, ICommand?>(nameof(Command));

    /// <summary>Gets or sets the command run with the double-tapped row's <see cref="StyledElement.DataContext"/> as its parameter.</summary>
    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    /// <inheritdoc/>
    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject?.AddHandler(InputElement.DoubleTappedEvent, OnDoubleTapped, RoutingStrategies.Bubble);
    }

    /// <inheritdoc/>
    protected override void OnDetaching()
    {
        AssociatedObject?.RemoveHandler(InputElement.DoubleTappedEvent, OnDoubleTapped);
        base.OnDetaching();
    }

    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (Command is not { } command || e.Source is not Visual source)
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
                    e.Handled = true;
                }

                return;
            }
        }
    }
}
