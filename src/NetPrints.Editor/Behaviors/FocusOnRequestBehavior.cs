using Avalonia;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.Xaml.Interactivity;

namespace NetPrints.Editor.Behaviors;

/// <summary>
/// Focuses the attached element each time <see cref="Request"/> changes after the behavior is attached, once the pending layout
/// has run (D11 "no prebuilt fits, custom second": a view model that rebuilds its rows asks for the keyboard back with a counter).
/// </summary>
public sealed class FocusOnRequestBehavior : StyledElementBehavior<InputElement>
{
    /// <summary>Identifies <see cref="Request"/>.</summary>
    public static readonly StyledProperty<int> RequestProperty =
        AvaloniaProperty.Register<FocusOnRequestBehavior, int>(nameof(Request));

    /// <summary>Gets or sets the counter; each change focuses the element.</summary>
    public int Request
    {
        get => GetValue(RequestProperty);
        set => SetValue(RequestProperty, value);
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == RequestProperty && AssociatedObject is { } element)
        {
            Dispatcher.UIThread.Post(() => element.Focus(), DispatcherPriority.Loaded);
        }
    }
}
