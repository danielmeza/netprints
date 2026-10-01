using NetPrints.Editor.Contributions;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Commands;

/// <summary>The <c>redo</c> command: redoes the last undone command of the active document's class, or of the tree selection's class when no document is active.</summary>
public sealed class RedoCommandHandler : ICommandHandler
{
    /// <inheritdoc/>
    public bool CanExecute(CommandContext context) => UndoCommandHandler.StackOf(context) is { CanRedo: true };

    /// <inheritdoc/>
    public string? DynamicLabel(CommandContext context) => UndoCommandHandler.StackOf(context)?.RedoName is { } name ? $"Redo {name}" : "Redo";

    /// <inheritdoc/>
    public Task ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        UndoCommandHandler.StackOf(context)?.Redo();
        return Task.CompletedTask;
    }
}
