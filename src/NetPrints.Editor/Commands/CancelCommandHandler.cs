using NetPrints.Editor.Contributions;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Commands;

/// <summary>The <c>cancel</c> command: closes the open popup of the active graph; enabled only while one is open, so Esc otherwise reaches Nodify.</summary>
public sealed class CancelCommandHandler : ICommandHandler
{
    /// <inheritdoc/>
    public bool CanExecute(CommandContext context) => context.ActiveGraph is { HasOpenPopup: true };

    /// <inheritdoc/>
    public Task ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        context.ActiveGraph?.ClosePopups();
        return Task.CompletedTask;
    }
}
