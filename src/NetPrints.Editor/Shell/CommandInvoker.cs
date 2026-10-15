using NetPrints.Editor.Contributions;
using NetPrints.Editor.Hosting;

namespace NetPrints.Editor.Shell;

/// <summary>
/// Runs registered commands for the key bindings of a window: lists the commands whose gestures are active in a
/// scope, and runs one against a fresh <see cref="CommandContext"/> only when its handler is enabled then.
/// </summary>
/// <param name="registry">The registry whose commands are run.</param>
/// <param name="contexts">Builds the context of each invocation.</param>
/// <param name="onFaulted">Receives the exception of a command that fails.</param>
public sealed class CommandInvoker(IContributionRegistry registry, ICommandContextProvider contexts, Action<Exception> onFaulted)
{
    /// <summary>Gets the commands that have a gesture and are active in <paramref name="scope"/>.</summary>
    /// <param name="scope">The scope; <see cref="CommandScope.Global"/> returns only the shell-wide commands.</param>
    /// <returns>The commands, in registration order.</returns>
    public IReadOnlyList<CommandDescriptor> CommandsIn(CommandScope scope) =>
        [.. registry.Commands.Where(command => command.DefaultGestures is { Count: > 0 } && InScope(command.Scope, scope))];

    /// <summary>Gets the commands the context-menu items of a target run, in item order.</summary>
    /// <param name="target">The item a context menu opens on.</param>
    /// <returns>The commands; an item naming an unregistered command is left out.</returns>
    public IReadOnlyList<CommandDescriptor> ContextMenuCommands(ContextMenuTarget target) =>
    [
        .. registry.ContextMenuItems
            .Where(item => item.Target == target)
            .OrderBy(item => item.Order)
            .Select(item => registry.Commands.FirstOrDefault(command => command.Id == item.CommandId))
            .OfType<CommandDescriptor>(),
    ];

    /// <summary>Gets the tooltip the registered providers give a target.</summary>
    /// <param name="target">What the pointer is over.</param>
    /// <returns>The first provider's content, or null.</returns>
    public TooltipContent? TooltipFor(TooltipTarget target) => TooltipResolver.Resolve(registry.TooltipProviders, target);

    /// <summary>Raised when the enabled state or label of any command may have changed (see <see cref="ICommandContextProvider.CommandStatesChanged"/>).</summary>
    public event EventHandler? CommandStatesChanged
    {
        add => contexts.CommandStatesChanged += value;
        remove => contexts.CommandStatesChanged -= value;
    }

    /// <summary>Gets whether a command's handler is enabled for a context built now.</summary>
    /// <param name="command">The command.</param>
    /// <param name="scope">The scope the invocation would come from.</param>
    /// <param name="parameter">The command parameter, or null.</param>
    /// <returns><see langword="true"/> when <see cref="TryRun(CommandDescriptor, CommandScope, object?)"/> would start it.</returns>
    public bool CanRun(CommandDescriptor command, CommandScope scope = CommandScope.Global, object? parameter = null)
    {
        ArgumentNullException.ThrowIfNull(command);
        return command.Handler.CanExecute(contexts.Create(parameter, scope));
    }

    /// <summary>Builds the context of an invocation made now.</summary>
    /// <param name="scope">The scope the invocation comes from.</param>
    /// <param name="parameter">The command parameter, or null.</param>
    /// <returns>A fresh context.</returns>
    public CommandContext CreateContext(CommandScope scope = CommandScope.Global, object? parameter = null) => contexts.Create(parameter, scope);

    /// <summary>Runs a command if its handler is enabled for a context built now.</summary>
    /// <param name="command">The command.</param>
    /// <param name="scope">The scope the invocation comes from: the surface whose key was pressed or whose menu was used.</param>
    /// <param name="parameter">The command parameter, or null.</param>
    /// <returns><see langword="true"/> when the command was started, <see langword="false"/> when it is disabled.</returns>
    public bool TryRun(CommandDescriptor command, CommandScope scope = CommandScope.Global, object? parameter = null)
    {
        ArgumentNullException.ThrowIfNull(command);
        CommandContext context = contexts.Create(parameter, scope);
        if (!command.Handler.CanExecute(context))
        {
            return false;
        }

        RunAsync(command, context).Forget(onFaulted);
        return true;
    }

    /// <summary>Runs the registered command with the given name, if its handler is enabled for a context built now.</summary>
    /// <param name="commandName">The command's name without <see cref="ContributionIds.CommandPrefix"/>.</param>
    /// <param name="scope">The scope the invocation comes from.</param>
    /// <param name="parameter">The command parameter, or null.</param>
    /// <returns><see langword="true"/> when the command was started, <see langword="false"/> when it is unknown or disabled.</returns>
    public bool TryRun(string commandName, CommandScope scope = CommandScope.Global, object? parameter = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(commandName);
        CommandDescriptor? command = registry.Commands.FirstOrDefault(c => c.Id == ContributionIds.CommandPrefix + commandName);
        return command is not null && TryRun(command, scope, parameter);
    }

    private static async Task RunAsync(CommandDescriptor command, CommandContext context) =>
        await command.Handler.ExecuteAsync(context, CancellationToken.None).ConfigureAwait(true);

    private static bool InScope(CommandScope commandScope, CommandScope scope) =>
        scope == CommandScope.Global ? commandScope == CommandScope.Global : commandScope.HasFlag(scope);
}
