using NetPrints.Editor.Contributions;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Commands;

/// <summary>The <c>floatDocument</c> command.</summary>
public sealed class FloatDocumentCommandHandler : ICommandHandler
{
    /// <inheritdoc/>
    public bool CanExecute(CommandContext context) => context.ActiveDocument is { } id && !context.Shell.IsFloating(id);

    /// <inheritdoc/>
    public Task ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        if (context.ActiveDocument is { } id)
        {
            context.Shell.FloatDocument(id);
        }

        return Task.CompletedTask;
    }
}
