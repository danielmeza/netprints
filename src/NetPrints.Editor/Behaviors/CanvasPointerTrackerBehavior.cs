using Avalonia;
using Avalonia.Controls;
using Avalonia.Xaml.Interactivity;
using NetPrints.Editor.Controls;

namespace NetPrints.Editor.Behaviors;

/// <summary>
/// Starts the <see cref="CanvasPointerTracker"/> of the top level that holds the attached control as soon as the control joins
/// the visual tree, so the tracker does not miss the very pointer event that opens the first <see cref="CanvasPopup"/> (ADR-0004).
/// </summary>
internal sealed class CanvasPointerTrackerBehavior : StyledElementBehavior<Control>
{
    /// <inheritdoc/>
    protected override void OnAttached()
    {
        base.OnAttached();
        if (AssociatedObject is { } control)
        {
            control.AttachedToVisualTree += OnAttachedToVisualTree;
            Track(control);
        }
    }

    /// <inheritdoc/>
    protected override void OnDetaching()
    {
        if (AssociatedObject is { } control)
        {
            control.AttachedToVisualTree -= OnAttachedToVisualTree;
        }

        base.OnDetaching();
    }

    private static void Track(Control control)
    {
        if (TopLevel.GetTopLevel(control) is { } topLevel)
        {
            CanvasPointerTracker.For(topLevel);
        }
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (AssociatedObject is { } control)
        {
            Track(control);
        }
    }
}
