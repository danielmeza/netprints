using NetPrints.Editor.Contributions;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Commands;

/// <summary>The <c>projectSettings</c> command: shows the project settings.</summary>
public sealed class ProjectSettingsCommandHandler : ICommandHandler
{
    /// <inheritdoc/>
    public bool CanExecute(CommandContext context) => context.Session is not null;

    /// <inheritdoc/>
    public Task ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        context.Shell.ProjectActions.ShowProjectSettings();
        return Task.CompletedTask;
    }
}
