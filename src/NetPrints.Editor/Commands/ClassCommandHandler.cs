using NetPrints.Core;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Commands;

/// <summary>Base of the commands that act on one class: the tree selection's class, else the active document's.</summary>
/// <param name="act">What the command does to the class.</param>
public abstract class ClassCommandHandler(Action<IProjectActions, ClassGraph> act) : ICommandHandler
{
    /// <inheritdoc/>
    public bool CanExecute(CommandContext context) => TargetOf(context) is not null;

    /// <inheritdoc/>
    public Task ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        if (TargetOf(context) is { } cls)
        {
            act(context.Shell.ProjectActions, cls);
        }

        return Task.CompletedTask;
    }

    private static ClassGraph? TargetOf(CommandContext context) =>
        context.Session is null ? null : CommandTargets.TreeSelectionClass(context) ?? CommandTargets.ActiveDocumentClass(context);
}
