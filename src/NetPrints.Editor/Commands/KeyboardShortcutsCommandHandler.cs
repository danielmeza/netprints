using NetPrints.Editor.Contributions;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Commands;

/// <summary>The <c>keyboardShortcuts</c> command: shows every registered command with its shortcuts.</summary>
public sealed class KeyboardShortcutsCommandHandler : ICommandHandler
{
    /// <inheritdoc/>
    public bool CanExecute(CommandContext context) => true;

    /// <inheritdoc/>
    public Task ExecuteAsync(CommandContext context, CancellationToken cancellationToken) =>
        context.Shell.ProjectActions.ShowKeyboardShortcutsAsync(cancellationToken);
}
