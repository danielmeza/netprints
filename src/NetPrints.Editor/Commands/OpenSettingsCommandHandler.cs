using NetPrints.Editor.Contributions;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Commands;

/// <summary>The <c>openSettings</c> command: opens the Settings document, or activates it when it is open. It needs no project.</summary>
public sealed class OpenSettingsCommandHandler : IEditorCommandHandler
{
    /// <inheritdoc/>
    public bool CanExecute(CommandContext context) => true;

    /// <inheritdoc/>
    public Task ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        context.Shell.OpenDocument(DocumentId.Settings);
        return Task.CompletedTask;
    }
}
