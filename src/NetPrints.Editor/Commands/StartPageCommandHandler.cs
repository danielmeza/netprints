using NetPrints.Editor.Contributions;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Commands;

/// <summary>The <c>startPage</c> command: opens the start page document, with or without a project open.</summary>
public sealed class StartPageCommandHandler : ICommandHandler
{
    /// <inheritdoc/>
    public bool CanExecute(CommandContext context) => true;

    /// <inheritdoc/>
    public Task ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        context.Shell.OpenDocument(DocumentId.StartPage);
        return Task.CompletedTask;
    }
}
