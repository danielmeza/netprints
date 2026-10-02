using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Xaml.Interactivity;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Behaviors;

/// <summary>
/// Runs the invoker's commands of one scope from key presses inside the attached element, in the tunnel phase so the
/// canvas or tree sees the key before its children. A key pressed in a text input is left to that input.
/// </summary>
public sealed class ScopedCommandKeysBehavior : StyledElementBehavior<InputElement>
{
    /// <summary>Identifies <see cref="Invoker"/>.</summary>
    public static readonly StyledProperty<CommandInvoker?> InvokerProperty =
        AvaloniaProperty.Register<ScopedCommandKeysBehavior, CommandInvoker?>(nameof(Invoker));

    /// <summary>Identifies <see cref="Scope"/>.</summary>
    public static readonly StyledProperty<CommandScope> ScopeProperty =
        AvaloniaProperty.Register<ScopedCommandKeysBehavior, CommandScope>(nameof(Scope), CommandScope.Graph);

    /// <summary>Gets or sets the invoker whose commands are run.</summary>
    public CommandInvoker? Invoker
    {
        get => GetValue(InvokerProperty);
        set => SetValue(InvokerProperty, value);
    }

    /// <summary>Gets or sets the scope whose commands are run.</summary>
    public CommandScope Scope
    {
        get => GetValue(ScopeProperty);
        set => SetValue(ScopeProperty, value);
    }

    /// <inheritdoc/>
    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject?.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    /// <inheritdoc/>
    protected override void OnDetaching()
    {
        AssociatedObject?.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
        base.OnDetaching();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (Invoker is not { } invoker || CommandKeyGestures.IsInsideValueEditor(e.Source))
        {
            return;
        }

        foreach (CommandDescriptor command in invoker.CommandsIn(Scope))
        {
            if (CommandKeyGestures.Of(command).Any(gesture => gesture.Matches(e)) && invoker.TryRun(command))
            {
                e.Handled = true;
                return;
            }
        }
    }
}
