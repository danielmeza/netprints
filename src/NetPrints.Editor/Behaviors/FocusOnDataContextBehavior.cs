using Avalonia;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactivity;

namespace NetPrints.Editor.Behaviors;

/// <summary>
/// Focuses the attached element, once the pending layout and visibility changes have been applied, each time it has a
/// non-null <c>DataContext</c> and either the <c>DataContext</c> changes, the element joins the visual tree (again) or it
/// is made visible (D11 "no prebuilt fits, custom second": the prebuilt data-context trigger fires before the element is
/// visible, so the focus is lost). Used on the graph canvas so that opening a graph, or switching back to its tab whether
/// the tab keeps its view or builds a new one, makes its shortcuts work without a click.
/// </summary>
public sealed class FocusOnDataContextBehavior : StyledElementBehavior<InputElement>
{
    /// <inheritdoc/>
    protected override void OnAttached()
    {
        base.OnAttached();
        if (AssociatedObject is { } element)
        {
            element.DataContextChanged += OnDataContextChanged;
            element.AttachedToVisualTree += OnAttachedToVisualTree;
            element.PropertyChanged += OnElementPropertyChanged;
            RequestFocus();
        }
    }

    /// <inheritdoc/>
    protected override void OnDetaching()
    {
        if (AssociatedObject is { } element)
        {
            element.DataContextChanged -= OnDataContextChanged;
            element.AttachedToVisualTree -= OnAttachedToVisualTree;
            element.PropertyChanged -= OnElementPropertyChanged;
        }

        base.OnDetaching();
    }

    private void OnDataContextChanged(object? sender, EventArgs e) => RequestFocus();

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e) => RequestFocus();

    private void OnElementPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Visual.IsVisibleProperty && e.NewValue is true)
        {
            RequestFocus();
        }
    }

    private void RequestFocus()
    {
        if (AssociatedObject is { DataContext: not null } element)
        {
            Dispatcher.UIThread.Post(() => element.Focus(), DispatcherPriority.Loaded);
        }
    }
}
