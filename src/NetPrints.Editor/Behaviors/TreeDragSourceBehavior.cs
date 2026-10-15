using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Xaml.Interactivity;
using NetPrints.Editor.Graph;
using NetPrints.Editor.ProjectTree;

namespace NetPrints.Editor.Behaviors;

/// <summary>
/// Makes a project tree row a drag source for the graph canvas: pressing the left button on a row whose
/// <see cref="ProjectTreeItemViewModel.CanDrag"/> is true and moving the pointer a few pixels starts the drag
/// (FR-017, PAR-56, PAR-57). Clicks and double clicks keep working; other rows start no drag.
/// </summary>
public sealed class TreeDragSourceBehavior : StyledElementBehavior<Control>
{
    private readonly DragSourceHelper dragSource = new();

    /// <inheritdoc/>
    protected override void OnAttached()
    {
        base.OnAttached();
        if (AssociatedObject is { } element)
        {
            element.PointerPressed += OnPointerPressed;
            element.PointerMoved += OnPointerMoved;
            element.PointerReleased += OnPointerReleased;
        }
    }

    /// <inheritdoc/>
    protected override void OnDetaching()
    {
        if (AssociatedObject is { } element)
        {
            element.PointerPressed -= OnPointerPressed;
            element.PointerMoved -= OnPointerMoved;
            element.PointerReleased -= OnPointerReleased;
        }

        base.OnDetaching();
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (AssociatedObject is { DataContext: ProjectTreeItemViewModel { CanDrag: true } row } element)
        {
            dragSource.Pressed(e, element, row);
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
