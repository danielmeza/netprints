using NetPrints.Editor.Contributions;
using NetPrints.Editor.Shell;
using NetPrints.Editor.UndoRedo;

namespace NetPrints.Editor.Commands;

/// <summary>The <c>undo</c> command: undoes the last command of the active document's class, or of the tree selection's class when no document is active.</summary>
public sealed class UndoCommandHandler : ICommandHandler
{
    /// <inheritdoc/>
    public bool CanExecute(CommandContext context) => StackOf(context) is { CanUndo: true };

    /// <inheritdoc/>
    public string? DynamicLabel(CommandContext context) => StackOf(context)?.UndoName is { } name ? $"Undo {name}" : "Undo";

    /// <inheritdoc/>
    public Task ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        StackOf(context)?.Undo();
        return Task.CompletedTask;
    }

    internal static UndoRedoStack? StackOf(CommandContext context) =>
        (CommandTargets.ActiveDocumentClass(context) ?? CommandTargets.TreeSelectionClass(context)) is { } cls
            ? context.Session?.UndoStackFor(cls)
            : null;
}
