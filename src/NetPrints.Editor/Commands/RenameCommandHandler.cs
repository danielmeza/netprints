using NetPrints.Core;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Commands;

/// <summary>The <c>rename</c> command: renames the selected project tree item, or else the active graph; a constructor is never renamed.</summary>
public sealed class RenameCommandHandler : ICommandHandler
{
    /// <inheritdoc/>
    public bool CanExecute(CommandContext context) => TargetOf(context) is { } target && target is not ConstructorGraph;

    /// <inheritdoc/>
    public Task ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        if (TargetOf(context) is { } target)
        {
            context.Shell.ProjectActions.RenameItem(target);
        }

        return Task.CompletedTask;
    }

    private static object? TargetOf(CommandContext context) => context.Selection.TreeItem ?? context.ActiveGraph?.Graph;
}
