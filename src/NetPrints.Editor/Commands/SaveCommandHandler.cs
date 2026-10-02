using NetPrints.Editor.Contributions;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Commands;

/// <summary>The <c>save</c> command: saves the edited classes of the open project (every edited class until the shell tracks the unsaved files per document).</summary>
public sealed class SaveCommandHandler : ICommandHandler
{
    /// <inheritdoc/>
    public bool CanExecute(CommandContext context) => context.Session is not null;

    /// <inheritdoc/>
    public Task ExecuteAsync(CommandContext context, CancellationToken cancellationToken) =>
        context.Session?.SaveAllAsync() ?? Task.CompletedTask;
}
