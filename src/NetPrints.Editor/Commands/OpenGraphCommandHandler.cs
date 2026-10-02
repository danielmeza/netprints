using NetPrints.Editor.Contributions;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Commands;

/// <summary>The <c>openGraph</c> command: opens the graph selected in the project tree as a document.</summary>
public sealed class OpenGraphCommandHandler : ICommandHandler
{
    /// <inheritdoc/>
    public bool CanExecute(CommandContext context) => CommandTargets.TreeSelectionDocument(context) is not null;

    /// <inheritdoc/>
    public Task ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        if (CommandTargets.TreeSelectionDocument(context) is { } id)
        {
            context.Shell.OpenDocument(id);
        }

        return Task.CompletedTask;
    }
}
