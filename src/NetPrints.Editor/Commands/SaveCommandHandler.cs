using NetPrints.Core;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Commands;

/// <summary>The <c>save</c> command: saves the file of the active graph's class; with no graph document active it saves every unsaved file.</summary>
public sealed class SaveCommandHandler : ICommandHandler
{
    /// <inheritdoc/>
    public bool CanExecute(CommandContext context) => context.Session is not null;

    /// <inheritdoc/>
    public Task ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        if (context.Session is not { } session)
        {
            return Task.CompletedTask;
        }

        ClassGraph? active = context.ActiveDocument is { Kind: DocumentKind.Graph, ClassPath: { } classPath } ? session.FindClass(classPath) : null;
        return active is null ? session.SaveAllAsync() : session.SaveAsync(active);
    }
}
