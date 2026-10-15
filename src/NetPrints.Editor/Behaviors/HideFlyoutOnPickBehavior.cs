using Avalonia;
using Avalonia.Controls;
using Avalonia.Xaml.Interactivity;
using NetPrints.Editor.Controls;

namespace NetPrints.Editor.Behaviors;

/// <summary>
/// Hides the flyout of the attached button when the bound <see cref="MethodPickerListViewModel"/> picks a method or is
/// cancelled. No prebuilt action fits: <c>HideFlyoutAction</c> looks for an attached flyout, not for <c>Button.Flyout</c>.
/// </summary>
public sealed class HideFlyoutOnPickBehavior : StyledElementBehavior<Button>
{
    /// <summary>Identifies <see cref="Picker"/>.</summary>
    public static readonly StyledProperty<MethodPickerListViewModel?> PickerProperty =
        AvaloniaProperty.Register<HideFlyoutOnPickBehavior, MethodPickerListViewModel?>(nameof(Picker));

    /// <summary>Gets or sets the list whose <see cref="MethodPickerListViewModel.Picked"/> and <see cref="MethodPickerListViewModel.Cancelled"/> close the flyout.</summary>
    public MethodPickerListViewModel? Picker
    {
        get => GetValue(PickerProperty);
        set => SetValue(PickerProperty, value);
    }

    /// <inheritdoc/>
    protected override void OnDetaching()
    {
        Unsubscribe(Picker);
        base.OnDetaching();
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PickerProperty)
        {
            Unsubscribe(change.GetOldValue<MethodPickerListViewModel?>());
            Subscribe(change.GetNewValue<MethodPickerListViewModel?>());
        }
    }

    private void Subscribe(MethodPickerListViewModel? picker)
    {
        if (picker is not null)
        {
            picker.Picked += OnPicked;
            picker.Cancelled += OnCancelled;
        }
    }

    private void Unsubscribe(MethodPickerListViewModel? picker)
    {
        if (picker is not null)
        {
            picker.Picked -= OnPicked;
            picker.Cancelled -= OnCancelled;
        }
    }

    private void OnPicked(object? sender, MethodPickerItem item) => AssociatedObject?.Flyout?.Hide();

    private void OnCancelled(object? sender, EventArgs e) => AssociatedObject?.Flyout?.Hide();
}
