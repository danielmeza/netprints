using NetPrints.Editor.Contributions;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Commands;

/// <summary>The <c>nodeSearch</c> command: asks the canvas to open the node search.</summary>
public sealed class NodeSearchCommandHandler : ICommandHandler
{
    /// <inheritdoc/>
    public bool CanExecute(CommandContext context) => context.ActiveGraph is not null;

    /// <inheritdoc/>
    public Task ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        context.ActiveGraph?.RequestView(GraphViewRequest.NodeSearch);
        return Task.CompletedTask;
    }
}
