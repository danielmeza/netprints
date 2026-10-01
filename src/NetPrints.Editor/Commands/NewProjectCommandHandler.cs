using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Commands;

/// <summary>The <c>newProject</c> command: creates a project and opens it.</summary>
public sealed class NewProjectCommandHandler : UnloadingCommandHandler
{
    /// <inheritdoc/>
    protected override Task RunAsync(CommandContext context, CancellationToken cancellationToken) =>
        context.Shell.ProjectActions.NewProjectAsync(cancellationToken);
}
