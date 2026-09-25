namespace NetPrints.Editor.Commands;

/// <summary>
/// An action that can be executed and undone.
/// </summary>
public interface IUndoableCommand
{
    string Name { get; }

    void Execute();

    void Undo();
}

/// <summary>
/// Undo/redo history of one class editor (the WPF editor used a global singleton).
/// </summary>
public sealed class UndoRedoStack
{
    private readonly Stack<IUndoableCommand> undoStack = new();
    private readonly Stack<IUndoableCommand> redoStack = new();

    public bool CanUndo => undoStack.Count > 0;

    public bool CanRedo => redoStack.Count > 0;

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
    public string Name { get; } = name;

    public void Execute() => execute();

    public void Undo() => undo();
}
