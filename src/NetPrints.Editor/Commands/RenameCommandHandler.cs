using NetPrints.Core;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Variables;

namespace NetPrints.Editor.Commands;

/// <summary>
/// The <c>rename</c> command: renames the selected project tree item when invoked from the tree, and the member of the
/// active graph otherwise (the variable for an accessor or type graph), falling back to the tree item only when no graph
/// is active. A constructor is never renamed. From the Variables panel it starts the in-place rename of the row passed as the command parameter.
/// </summary>
public sealed class RenameCommandHandler : ICommandHandler
{
    /// <inheritdoc/>
    public bool CanExecute(CommandContext context) => context.Scope == CommandScope.VariablesPanel
        ? context.Parameter is VariableRowViewModel row && row.BeginRenameCommand.CanExecute(null)
        : TargetOf(context) is { } target && target is not ConstructorGraph;

    /// <inheritdoc/>
    public Task ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        if (context.Scope == CommandScope.VariablesPanel)
        {
            if (context.Parameter is VariableRowViewModel row && row.BeginRenameCommand.CanExecute(null))
            {
                row.BeginRenameCommand.Execute(null);
            }
        }
        else if (TargetOf(context) is { } target)
        {
            context.Shell.ProjectActions.RenameItem(target);
        }

        return Task.CompletedTask;
    }

    private static object? TargetOf(CommandContext context) => context.Scope == CommandScope.ProjectTree
        ? context.Selection.TreeItem
        : context.ActiveGraph?.Graph is { } graph ? CommandTargets.MemberOf(graph) : context.Selection.TreeItem;
}
