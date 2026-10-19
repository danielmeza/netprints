using Avalonia;
using Avalonia.Controls;

namespace Fx.Views;

/// <summary>A view that is built fine and throws when it is measured.</summary>
public sealed class MeasureThrowingView : Control
{
    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize) => throw new InvalidOperationException("view measure");
}
