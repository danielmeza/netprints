using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Commands;

/// <summary>The <c>openFolder</c> command: asks for a folder with the folder picker and opens the one project it holds.</summary>
public sealed class OpenFolderCommandHandler : UnloadingCommandHandler
{
    /// <inheritdoc/>
    protected override Task RunAsync(CommandContext context, CancellationToken cancellationToken) =>
        context.Shell.ProjectActions.OpenFolderAsync(cancellationToken);
}
