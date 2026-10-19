using System.Windows.Input;
using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.Xaml.Interactivity;
using NetPrints.Extensibility.Editor;

namespace NetPrints.Editor.Behaviors;

/// <summary>
/// While <see cref="IsRecording"/> is on, turns the next key chord pressed inside the attached element into a gesture
/// text and passes it to <see cref="Command"/>; Escape passes it to <see cref="CancelCommand"/>. Modifier keys alone are ignored.
/// Recording also ends when the keyboard focus leaves the element or the element leaves the visual tree.
/// </summary>
public sealed class RecordGestureBehavior : StyledElementBehavior<InputElement>
{
    /// <summary>Identifies <see cref="IsRecording"/>.</summary>
    public static readonly StyledProperty<bool> IsRecordingProperty =
        AvaloniaProperty.Register<RecordGestureBehavior, bool>(nameof(IsRecording));

    /// <summary>Identifies <see cref="Command"/>.</summary>
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<RecordGestureBehavior, ICommand?>(nameof(Command));

    /// <summary>Identifies <see cref="CancelCommand"/>.</summary>
    public static readonly StyledProperty<ICommand?> CancelCommandProperty =
        AvaloniaProperty.Register<RecordGestureBehavior, ICommand?>(nameof(CancelCommand));

    /// <summary>Gets or sets a value indicating whether key presses are recorded.</summary>
    public bool IsRecording
    {
        get => GetValue(IsRecordingProperty);
        set => SetValue(IsRecordingProperty, value);
    }

    /// <summary>Gets or sets the command that receives the gesture text.</summary>
    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    /// <summary>Gets or sets the command run when Escape ends the recording.</summary>
    public ICommand? CancelCommand
    {
        get => GetValue(CancelCommandProperty);
        set => SetValue(CancelCommandProperty, value);
    }

    /// <summary>Converts a key press to the canonical gesture text.</summary>
    /// <param name="key">The Avalonia key.</param>
    /// <param name="modifiers">The modifiers held.</param>
    /// <param name="isMacOS">Whether Command maps to <c>Ctrl</c>.</param>
    /// <returns>The text, or <see langword="null"/> for a modifier key alone or a key no gesture can name.</returns>
    public static string? GestureText(Key key, KeyModifiers modifiers, bool isMacOS)
    {
        if (!CommandKey.TryParse(key.ToString(), null, out CommandKey? parsed))
        {
            return null;
        }

        CommandModifiers result = CommandModifiers.None;
        bool control = modifiers.HasFlag(KeyModifiers.Control) || (isMacOS && modifiers.HasFlag(KeyModifiers.Meta));
        result |= control ? CommandModifiers.Ctrl : CommandModifiers.None;
        result |= modifiers.HasFlag(KeyModifiers.Alt) ? CommandModifiers.Alt : CommandModifiers.None;
        result |= modifiers.HasFlag(KeyModifiers.Shift) ? CommandModifiers.Shift : CommandModifiers.None;
        result |= !isMacOS && modifiers.HasFlag(KeyModifiers.Meta) ? CommandModifiers.Meta : CommandModifiers.None;
        return new CommandGesture(result, parsed).ToString();
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsRecordingProperty && change.GetNewValue<bool>() && AssociatedObject is { } element)
        {
            element.Focus();
        }
    }

    /// <inheritdoc/>
    protected override void OnAttached()
    {
        base.OnAttached();
        if (AssociatedObject is { } element)
        {
            element.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
            element.PropertyChanged += OnElementPropertyChanged;
            element.DetachedFromVisualTree += OnDetachedFromVisualTree;
        }
    }

    /// <inheritdoc/>
    protected override void OnDetaching()
    {
        if (AssociatedObject is { } element)
        {
            element.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
            element.PropertyChanged -= OnElementPropertyChanged;
            element.DetachedFromVisualTree -= OnDetachedFromVisualTree;
        }

        base.OnDetaching();
    }

    private void OnElementPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == InputElement.IsKeyboardFocusWithinProperty && e.NewValue is false && IsRecording)
        {
            Dispatcher.UIThread.Post(CancelIfFocusLeft, DispatcherPriority.Input);
        }
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (IsRecording)
        {
            CancelCommand?.Execute(null);
        }
    }

    private void CancelIfFocusLeft()
    {
        if (IsRecording && AssociatedObject is { IsKeyboardFocusWithin: false })
        {
            CancelCommand?.Execute(null);
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (!IsRecording)
        {
            return;
        }

        e.Handled = true;
        if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None)
        {
            CancelCommand?.Execute(null);
            return;
        }

        if (GestureText(e.Key, e.KeyModifiers, OperatingSystem.IsMacOS()) is { } text && Command?.CanExecute(text) == true)
        {
            Command.Execute(text);
        }
    }
}
