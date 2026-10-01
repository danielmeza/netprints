using NetPrints.Editor.Contributions;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Commands;

/// <summary>The <c>delete</c> command: deletes the selected nodes of the active graph, or else the selected project tree item.</summary>
public sealed class DeleteCommandHandler : ICommandHandler
{
    /// <inheritdoc/>
    public bool CanExecute(CommandContext context) => SelectedNodes(context) || context.Selection.TreeItem is not null;

    /// <inheritdoc/>
    public Task ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        if (SelectedNodes(context))
        {
            context.ActiveGraph?.DeleteSelectedNodes();
        }
        else if (context.Selection.TreeItem is { } item)
        {
            context.Shell.ProjectActions.DeleteItem(item);
        }

        return Task.CompletedTask;
    }

    private static bool SelectedNodes(CommandContext context) => context.ActiveGraph is not null && context.Selection.Nodes.Count > 0;
}
