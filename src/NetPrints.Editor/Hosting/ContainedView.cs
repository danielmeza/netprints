using Avalonia;
using Avalonia.Controls;

namespace NetPrints.Editor.Hosting;

/// <summary>
/// Holds a view an extension contributed (FR-034, research R27): when the view throws while it is measured or arranged, the
/// failure is handed to <c>contain</c> once, and the placeholder it returns takes the view's place.
/// </summary>
internal sealed class ContainedView : Decorator
{
    private readonly Func<Exception, Control> contain;
    private bool failed;

    /// <summary>Creates the holder.</summary>
    /// <param name="view">The extension's view.</param>
    /// <param name="contain">Reports the failure and builds the placeholder.</param>
    public ContainedView(Control view, Func<Exception, Control> contain)
    {
        ArgumentNullException.ThrowIfNull(contain);
        this.contain = contain;
        Child = view;
    }

    /// <summary>Gets a value indicating whether the view was replaced by the placeholder.</summary>
    public bool HasFailed => failed;

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        try
        {
            return base.MeasureOverride(availableSize);
        }
        catch (Exception ex) when (CanContain(ex))
        {
            Replace(ex);
            return base.MeasureOverride(availableSize);
        }
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        try
        {
            return base.ArrangeOverride(finalSize);
        }
        catch (Exception ex) when (CanContain(ex))
        {
            Replace(ex);
            Child?.Measure(finalSize);
            return base.ArrangeOverride(finalSize);
        }
    }

    private bool CanContain(Exception ex) => ex is not OperationCanceledException && !failed;

    private void Replace(Exception ex)
    {
        failed = true;
        Child = contain(ex);
    }
}
