using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Commands;

/// <summary>The <c>closeProject</c> command: closes the open project.</summary>
public sealed class CloseProjectCommandHandler : UnloadingCommandHandler
{
    /// <inheritdoc/>
    public override bool CanExecute(CommandContext context) => context.Session is not null;

    /// <inheritdoc/>
    protected override Task RunAsync(CommandContext context, CancellationToken cancellationToken) =>
        context.Shell.ProjectActions.CloseProjectAsync(cancellationToken);
}
