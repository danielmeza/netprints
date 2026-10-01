using NetPrints.Editor.Contributions;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Commands;

/// <summary>The <c>run</c> command: builds the open project, then starts the program; disabled while a program runs (Stop takes its place).</summary>
public sealed class RunCommandHandler : ICommandHandler
{
    /// <inheritdoc/>
    public bool CanExecute(CommandContext context) => context.Session is { IsRunning: false, Project.CanCompileAndRun: true };

    /// <inheritdoc/>
    public Task ExecuteAsync(CommandContext context, CancellationToken cancellationToken) =>
        context.Session?.RunAsync() ?? Task.CompletedTask;
}
