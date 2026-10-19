using Avalonia;
using Avalonia.Controls;

namespace Fx.Views;

/// <summary>A view that measures fine, and throws on the measure that follows <see cref="Trip"/>.</summary>
public sealed class LateThrowingView : Control
{
    private bool tripped;

    /// <summary>Makes the next measure throw and asks for it, so the layout manager measures this control directly.</summary>
    public void Trip()
    {
        tripped = true;
        InvalidateMeasure();
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize) =>
        tripped ? throw new InvalidOperationException("late measure") : new Size(10, 10);
}
