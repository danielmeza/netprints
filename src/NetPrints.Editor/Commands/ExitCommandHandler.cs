using NetPrints.Editor.Contributions;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Commands;

/// <summary>
/// The <c>exit</c> command: closes the main window. It asks nothing itself; the window-close path owns the unload
/// prompt, so Exit and the OS close ask once each.
/// </summary>
public sealed class ExitCommandHandler : ICommandHandler
{
    /// <inheritdoc/>
    public bool CanExecute(CommandContext context) => true;

    /// <inheritdoc/>
    public Task ExecuteAsync(CommandContext context, CancellationToken cancellationToken) =>
        context.Shell.ProjectActions.ExitAsync(cancellationToken);
}
