using NetPrints.Core;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Commands;

/// <summary>Base of the commands that act on one class: the tree selection's class, else the active document's.</summary>
public abstract class ClassCommandHandler : ICommandHandler
{
    private readonly Func<IProjectActions, ClassGraph, CancellationToken, Task> act;

    /// <summary>Creates a handler whose action completes at once.</summary>
    /// <param name="act">What the command does to the class.</param>
    protected ClassCommandHandler(Action<IProjectActions, ClassGraph> act)
    {
        ArgumentNullException.ThrowIfNull(act);
        this.act = (actions, cls, _) =>
        {
            act(actions, cls);
            return Task.CompletedTask;
        };
    }

    /// <summary>Creates a handler whose action is asynchronous.</summary>
    /// <param name="act">What the command does to the class.</param>
    protected ClassCommandHandler(Func<IProjectActions, ClassGraph, CancellationToken, Task> act)
    {
        ArgumentNullException.ThrowIfNull(act);
        this.act = act;
    }

    /// <inheritdoc/>
    public bool CanExecute(CommandContext context) => TargetOf(context) is not null;

    /// <inheritdoc/>
    public Task ExecuteAsync(CommandContext context, CancellationToken cancellationToken) =>
        TargetOf(context) is { } cls ? act(context.Shell.ProjectActions, cls, cancellationToken) : Task.CompletedTask;

    private static ClassGraph? TargetOf(CommandContext context) =>
        context.Session is null ? null : CommandTargets.TreeSelectionClass(context) ?? CommandTargets.ActiveDocumentClass(context);
}
