using Avalonia;
using Avalonia.Input;
using Avalonia.Xaml.Interactivity;
using CommunityToolkit.Mvvm.Input;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Behaviors;

/// <summary>Adds a key binding to the attached element for every gesture of the invoker's global commands.</summary>
public sealed class CommandKeyBindingsBehavior : StyledElementBehavior<InputElement>
{
    /// <summary>Identifies <see cref="Invoker"/>.</summary>
    public static readonly StyledProperty<CommandInvoker?> InvokerProperty =
        AvaloniaProperty.Register<CommandKeyBindingsBehavior, CommandInvoker?>(nameof(Invoker));

    private readonly List<KeyBinding> added = [];

    /// <summary>Gets or sets the invoker whose global commands are bound.</summary>
    public CommandInvoker? Invoker
    {
        get => GetValue(InvokerProperty);
        set => SetValue(InvokerProperty, value);
    }

    /// <inheritdoc/>
    protected override void OnAttached()
    {
        base.OnAttached();
        Rebuild();
    }

    /// <inheritdoc/>
    protected override void OnDetaching()
    {
        Clear();
        base.OnDetaching();
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == InvokerProperty)
        {
            Rebuild();
        }
    }

    private void Rebuild()
    {
        Clear();
        if (AssociatedObject is not { } element || Invoker is not { } invoker)
        {
            return;
        }

        foreach (CommandDescriptor command in invoker.CommandsIn(CommandScope.Global))
        {
            foreach (KeyGesture gesture in CommandKeyGestures.Of(command))
            {
                var binding = new KeyBinding { Gesture = gesture, Command = new RelayCommand(() => invoker.TryRun(command), () => command.Handler.CanExecute(invoker.CreateContext())) };
                added.Add(binding);
                element.KeyBindings.Add(binding);
            }
        }
    }

    private void Clear()
    {
        foreach (KeyBinding binding in added)
        {
            AssociatedObject?.KeyBindings.Remove(binding);
        }

        added.Clear();
    }
}
