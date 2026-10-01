using NetPrints.Editor.Contributions;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Commands;

/// <summary>The <c>cancel</c> command: closes the open popup of the active graph, or else clears its selection.</summary>
public sealed class CancelCommandHandler : ICommandHandler
{
    /// <inheritdoc/>
    public bool CanExecute(CommandContext context) => context.ActiveGraph is not null;

    /// <inheritdoc/>
    public Task ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        context.ActiveGraph?.Cancel();
        return Task.CompletedTask;
    }
}
