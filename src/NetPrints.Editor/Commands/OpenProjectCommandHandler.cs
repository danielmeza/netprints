using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Commands;

/// <summary>The <c>openProject</c> command: opens a project, asking for it with a file picker unless the parameter names its path (a <c>.csproj</c>, or a folder that holds exactly one).</summary>
public sealed class OpenProjectCommandHandler : UnloadingCommandHandler
{
    /// <inheritdoc/>
    protected override Task RunAsync(CommandContext context, CancellationToken cancellationToken) =>
        context.Shell.ProjectActions.OpenProjectAsync(context.Parameter as string, cancellationToken);
}
