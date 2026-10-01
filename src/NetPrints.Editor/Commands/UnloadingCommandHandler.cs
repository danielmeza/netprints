using NetPrints.Editor.Contributions;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Commands;

/// <summary>
/// Base of the commands that unload the open project (open another, create, close, exit): the single call site that
/// asks <see cref="IProjectActions.ConfirmUnloadAsync"/> first. With no project open there is nothing to unload and
/// nothing is asked.
/// </summary>
public abstract class UnloadingCommandHandler : ICommandHandler
{
    /// <inheritdoc/>
    public virtual bool CanExecute(CommandContext context) => true;

    /// <inheritdoc/>
    public async Task ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        if (context.Session is not null && !await context.Shell.ProjectActions.ConfirmUnloadAsync(cancellationToken).ConfigureAwait(true))
        {
            return;
        }

        await RunAsync(context, cancellationToken).ConfigureAwait(true);
    }

    /// <summary>Runs the flow once the project may be unloaded.</summary>
    /// <param name="context">The invocation context.</param>
    /// <param name="cancellationToken">Cancels the flow.</param>
    /// <returns>A task that completes when the flow has finished.</returns>
    protected abstract Task RunAsync(CommandContext context, CancellationToken cancellationToken);
}
