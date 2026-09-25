namespace NetPrints.Editor.UndoRedo;

/// <summary>
/// An action that can be executed and undone.
/// </summary>
public interface IUndoableCommand
{
    /// <summary>Human-readable name of the command (unused by <see cref="UndoRedoStack"/> itself; for diagnostics).</summary>
    string Name { get; }

    /// <summary>Performs the command's action.</summary>
    void Execute();

    /// <summary>Reverses the command's action.</summary>
    void Undo();
}

/// <summary>
/// Undo/redo history of one class editor (the WPF editor used a global singleton).
/// </summary>
public sealed class UndoRedoStack
{
    private readonly Stack<IUndoableCommand> undoStack = new();
    private readonly Stack<IUndoableCommand> redoStack = new();

    /// <summary>Whether <see cref="Undo"/> would undo a command.</summary>
    public bool CanUndo => undoStack.Count > 0;

    /// <summary>Whether <see cref="Redo"/> would redo a command.</summary>
    public bool CanRedo => redoStack.Count > 0;

    /// <summary>Raised after <see cref="Do"/>, <see cref="Undo"/>, <see cref="Redo"/> or <see cref="Clear"/> changes the history.</summary>
    public event EventHandler? Changed;

    /// <summary>Executes a command and records it. Clears the redo history (PAR-60).</summary>
    public void Do(IUndoableCommand command)
    {
        command.Execute();
        undoStack.Push(command);
        redoStack.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Undoes the last command. Returns whether a command was undone.</summary>
    public bool Undo()
    {
        if (!undoStack.TryPop(out var command))
        {
            return false;
        }

        command.Undo();
        redoStack.Push(command);
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>Redoes the last undone command. Returns whether a command was redone.</summary>
    public bool Redo()
    {
        if (!redoStack.TryPop(out var command))
        {
            return false;
        }

        command.Execute();
        undoStack.Push(command);
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>Clears the undo and redo history without executing or undoing anything.</summary>
    public void Clear()
    {
        undoStack.Clear();
        redoStack.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// An undoable command built from two delegates.
/// </summary>
public sealed class DelegateUndoableCommand(string name, Action execute, Action undo) : IUndoableCommand
{
    /// <inheritdoc/>
    public string Name { get; } = name;

    /// <summary>Invokes the <c>execute</c> delegate passed to the constructor.</summary>
    public void Execute() => execute();

    /// <summary>Invokes the <c>undo</c> delegate passed to the constructor.</summary>
    public void Undo() => undo();
}
