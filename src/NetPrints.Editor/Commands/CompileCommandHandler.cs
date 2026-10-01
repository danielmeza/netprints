using NetPrints.Editor.Contributions;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Commands;

/// <summary>The <c>compile</c> command: builds the open project once a save in progress has finished.</summary>
public sealed class CompileCommandHandler : ICommandHandler
{
    /// <inheritdoc/>
    public bool CanExecute(CommandContext context) => context.Session is { Project.CanCompile: true };

    /// <inheritdoc/>
    public Task ExecuteAsync(CommandContext context, CancellationToken cancellationToken) =>
        context.Session?.CompileAsync() ?? Task.CompletedTask;
}
