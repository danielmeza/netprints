using NetPrints.Editor.Contributions;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Commands;

/// <summary>The <c>commandPalette</c> command: opens the command palette.</summary>
public sealed class CommandPaletteCommandHandler : ICommandHandler
{
    /// <inheritdoc/>
    public bool CanExecute(CommandContext context) => true;

    /// <inheritdoc/>
    public Task ExecuteAsync(CommandContext context, CancellationToken cancellationToken) =>
        context.Shell.ProjectActions.ShowCommandPaletteAsync(cancellationToken);
}
