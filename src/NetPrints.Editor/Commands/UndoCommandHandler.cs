using NetPrints.Core;
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
        if (ClassOf(context) is { } cls)
        {
            context.Session?.Undo(cls);
        }

        return Task.CompletedTask;
    }

    internal static ClassGraph? ClassOf(CommandContext context) =>
        CommandTargets.ActiveDocumentClass(context) ?? CommandTargets.TreeSelectionClass(context);

    internal static UndoRedoStack? StackOf(CommandContext context) =>
        ClassOf(context) is { } cls ? context.Session?.UndoStackFor(cls) : null;
}
