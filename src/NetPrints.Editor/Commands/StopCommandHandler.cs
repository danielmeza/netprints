using NetPrints.Editor.Contributions;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Commands;

/// <summary>The <c>stop</c> command: cancels the run's token, which kills the program and its child processes; enabled only while the program runs.</summary>
public sealed class StopCommandHandler : ICommandHandler
{
    /// <inheritdoc/>
    public bool CanExecute(CommandContext context) => context.Session is { IsRunning: true };

    /// <inheritdoc/>
    public Task ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        context.Session?.Stop();
        return Task.CompletedTask;
    }
}
