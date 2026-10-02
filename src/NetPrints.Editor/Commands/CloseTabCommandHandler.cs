using NetPrints.Editor.Contributions;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Commands;

/// <summary>The <c>closeTab</c> command.</summary>
public sealed class CloseTabCommandHandler : ICommandHandler
{
    /// <inheritdoc/>
    public bool CanExecute(CommandContext context) => context.ActiveDocument is not null;

    /// <inheritdoc/>
    public Task ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        if (context.ActiveDocument is { } id)
        {
            context.Shell.CloseDocument(id);
        }

        return Task.CompletedTask;
    }
}
