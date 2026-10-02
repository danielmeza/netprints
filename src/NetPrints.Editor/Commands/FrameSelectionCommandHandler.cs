using NetPrints.Editor.Contributions;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Commands;

/// <summary>The <c>frameSelection</c> command: asks the canvas to frame the selected nodes.</summary>
public sealed class FrameSelectionCommandHandler : ICommandHandler
{
    /// <inheritdoc/>
    public bool CanExecute(CommandContext context) => context.ActiveGraph is { } graph && graph.SelectedNodes.Any();

    /// <inheritdoc/>
    public Task ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        context.ActiveGraph?.RequestView(GraphViewRequest.FrameSelection);
        return Task.CompletedTask;
    }
}
