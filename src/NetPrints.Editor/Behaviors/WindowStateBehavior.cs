using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Xaml.Interactivity;
using NetPrints.Editor.State;
using AvaloniaWindowState = Avalonia.Controls.WindowState;

namespace NetPrints.Editor.Behaviors;

/// <summary>
/// Restores the attached window's bounds and maximized state from a <see cref="WindowStateService"/> once it is set, and saves
/// them when the window closes (FR-050, FR-051). It tracks the normal bounds while the window is not maximized, so a maximized
/// window comes back maximized over its previous normal size. The placement rule is <see cref="WindowPlacement.Resolve"/>.
/// </summary>
internal sealed class WindowStateBehavior : StyledElementBehavior<Window>
{
    /// <summary>Identifies <see cref="Service"/>.</summary>
    public static readonly StyledProperty<WindowStateService?> ServiceProperty =
        AvaloniaProperty.Register<WindowStateBehavior, WindowStateService?>(nameof(Service));

    private ScreenBounds? normalBounds;
    private bool isMaximized;
    private bool restored;
    private bool tracking;

    /// <summary>Gets or sets the service that restores and saves the state; nothing happens while it is null.</summary>
    public WindowStateService? Service
    {
        get => GetValue(ServiceProperty);
        set => SetValue(ServiceProperty, value);
    }

    /// <inheritdoc/>
    protected override void OnAttached()
    {
        base.OnAttached();
        if (AssociatedObject is { } window)
        {
            window.PositionChanged += OnPositionChanged;
            window.ScalingChanged += OnScalingChanged;
            window.PropertyChanged += OnWindowPropertyChanged;
            window.Opened += OnOpened;
            window.Closed += OnClosed;
            TryRestore(window);
        }
    }

    /// <inheritdoc/>
    protected override void OnDetaching()
    {
        if (AssociatedObject is { } window)
        {
            window.PositionChanged -= OnPositionChanged;
            window.ScalingChanged -= OnScalingChanged;
            window.PropertyChanged -= OnWindowPropertyChanged;
            window.Opened -= OnOpened;
            window.Closed -= OnClosed;
        }

        base.OnDetaching();
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ServiceProperty && AssociatedObject is { } window)
        {
            TryRestore(window);
        }
    }

    private static ScreenBounds ToBounds(Screen screen) => new(screen.WorkingArea.X, screen.WorkingArea.Y, screen.WorkingArea.Width, screen.WorkingArea.Height, screen.Scaling);

    private void TryRestore(Window window)
    {
        if (restored || Service is not { } service || window.Screens is not { } screens)
        {
            return;
        }

        restored = true;
        IReadOnlyList<ScreenBounds> areas = [.. screens.All.Select(ToBounds)];
        ScreenBounds? primary = screens.Primary is { } main ? ToBounds(main) : areas.FirstOrDefault();
        if (primary is null || service.Restore(areas, primary) is not { } placement)
        {
            return;
        }

        Apply(window, placement);
        normalBounds = placement.Bounds;
        isMaximized = placement.IsMaximized;
    }

    // The size is in pixels at the scaling of the screen the window lands on, which the window itself does not know before it is shown.
    internal static void Apply(Window window, WindowPlacement placement)
    {
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Position = new PixelPoint(placement.Bounds.X, placement.Bounds.Y);
        window.Width = placement.WidthInDips;
        window.Height = placement.HeightInDips;
        window.WindowState = placement.IsMaximized ? AvaloniaWindowState.Maximized : AvaloniaWindowState.Normal;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        tracking = true;
        if (AssociatedObject is { } window)
        {
            Track(window);
        }
    }

    private void OnScalingChanged(object? sender, EventArgs e)
    {
        if (AssociatedObject is { } window)
        {
            Track(window);
        }
    }

    private void OnPositionChanged(object? sender, PixelPointEventArgs e)
    {
        if (AssociatedObject is { } window)
        {
            Track(window);
        }
    }

    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (AssociatedObject is { } window && (e.Property == Visual.BoundsProperty || e.Property == Window.WindowStateProperty))
        {
            Track(window);
        }
    }

    private void Track(Window window)
    {
        if (!tracking)
        {
            return;
        }

        switch (window.WindowState)
        {
            case AvaloniaWindowState.Normal:
                double scaling = window.RenderScaling;
                normalBounds = new ScreenBounds(window.Position.X, window.Position.Y, (int)Math.Round(window.Bounds.Width * scaling), (int)Math.Round(window.Bounds.Height * scaling));
                isMaximized = false;
                break;
            case AvaloniaWindowState.Maximized:
                isMaximized = true;
                break;
            default:
                break;
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (Service is not { } service || normalBounds is not { } bounds)
        {
            return;
        }

        service.Save(bounds, isMaximized, null);
    }
}
