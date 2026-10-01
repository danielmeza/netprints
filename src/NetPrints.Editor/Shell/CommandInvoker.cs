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

    /// <summary>Builds the context of an invocation made now.</summary>
    /// <returns>A fresh context.</returns>
    public CommandContext CreateContext() => contexts.Create();

    /// <summary>Runs a command if its handler is enabled for a context built now.</summary>
    /// <param name="command">The command.</param>
    /// <returns><see langword="true"/> when the command was started, <see langword="false"/> when it is disabled.</returns>
    public bool TryRun(CommandDescriptor command)
    {
        ArgumentNullException.ThrowIfNull(command);
        CommandContext context = contexts.Create();
        if (!command.Handler.CanExecute(context))
        {
            return false;
        }

        RunAsync(command, context).Forget(onFaulted);
        return true;
    }

    private static async Task RunAsync(CommandDescriptor command, CommandContext context) =>
        await command.Handler.ExecuteAsync(context, CancellationToken.None).ConfigureAwait(true);

    private static bool InScope(CommandScope commandScope, CommandScope scope) =>
        scope == CommandScope.Global ? commandScope == CommandScope.Global : commandScope.HasFlag(scope);
}
