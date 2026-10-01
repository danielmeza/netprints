using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Commands;

/// <summary>The <c>exit</c> command: closes the application, through the same unload path as the window.</summary>
public sealed class ExitCommandHandler : UnloadingCommandHandler
{
    /// <inheritdoc/>
    protected override Task RunAsync(CommandContext context, CancellationToken cancellationToken) =>
        context.Shell.ProjectActions.ExitAsync(cancellationToken);
}
