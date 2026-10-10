using NetPrints.Editor.Contributions;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Commands;

/// <summary>The <c>fitAll</c> command: asks the canvas to fit every node.</summary>
public sealed class FitAllCommandHandler : ICommandHandler
{
    /// <inheritdoc/>
    public bool CanExecute(CommandContext context) => context.ActiveGraph is { Nodes.Count: > 0 };

    /// <inheritdoc/>
    public Task ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        context.ActiveGraph?.RequestView(GraphViewRequest.FitAll);
        return Task.CompletedTask;
    }
}
