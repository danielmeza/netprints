using NetPrints.Core;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Commands;

/// <summary>
/// The <c>delete</c> command, which acts only on the scope it was invoked from: the selected nodes of the active graph
/// for a key in the canvas or a menu, the selected class, method, constructor, event graph or variable for a key or a
/// context menu of the project tree. It never falls back from one to the other, so Delete on an empty canvas cannot
/// remove the member being edited.
/// </summary>
public sealed class DeleteCommandHandler : ICommandHandler
{
    /// <inheritdoc/>
    public bool CanExecute(CommandContext context) => context.Scope == CommandScope.ProjectTree
        ? context.Selection.TreeItem is ClassGraph or MethodGraph or ConstructorGraph or EventGraph or Variable
        : SelectedNodes(context);

    /// <inheritdoc/>
    public async Task ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        if (context.Scope == CommandScope.ProjectTree)
        {
            if (context.Selection.TreeItem is { } item)
            {
                await context.Shell.ProjectActions.DeleteItemAsync(item, cancellationToken).ConfigureAwait(true);
            }
        }
        else if (SelectedNodes(context))
        {
            context.ActiveGraph?.DeleteSelectedNodes();
        }
    }

    private static bool SelectedNodes(CommandContext context) => context.ActiveGraph is not null && context.Selection.Nodes.Count > 0;
}
