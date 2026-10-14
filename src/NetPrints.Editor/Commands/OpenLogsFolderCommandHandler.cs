using NetPrints.Editor.Contributions;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Commands;

/// <summary>The <c>help.openLogsFolder</c> command: shows the folder of the session logs in the file manager.</summary>
public sealed class OpenLogsFolderCommandHandler : ICommandHandler
{
    /// <inheritdoc/>
    public bool CanExecute(CommandContext context) => true;

    /// <inheritdoc/>
    public Task ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        context.Shell.ProjectActions.OpenLogsFolder();
        return Task.CompletedTask;
    }
}
