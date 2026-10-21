using Avalonia.Controls;

namespace Fx.Views;

/// <summary>A view whose constructor throws, so a template that builds it fails.</summary>
public sealed class ThrowingView : Control
{
    /// <summary>Throws.</summary>
    public ThrowingView() => throw new InvalidOperationException("view template");
}
