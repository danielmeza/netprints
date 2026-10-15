using NetPrints.Editor.Contributions;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Commands;

/// <summary>The <c>selectAll</c> command: selects every node of the active graph.</summary>
public sealed class SelectAllCommandHandler : ICommandHandler
{
    /// <inheritdoc/>
    public bool CanExecute(CommandContext context) => context.ActiveGraph is not null;

    /// <inheritdoc/>
    public Task ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        context.ActiveGraph?.SelectAll();
        return Task.CompletedTask;
    }
}
