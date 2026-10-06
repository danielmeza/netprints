namespace NetPrints.Editor.UndoRedo;

/// <summary>
/// An action that can be executed and undone.
/// </summary>
public interface IUndoableCommand
{
    /// <summary>Human-readable name of the command (shown as <see cref="UndoRedoStack.UndoName"/> and <see cref="UndoRedoStack.RedoName"/>).</summary>
    string Name { get; }

    /// <summary>Performs the command's action.</summary>
    void Execute();

    /// <summary>Reverses the command's action.</summary>
    void Undo();
}

/// <summary>A position of an <see cref="UndoRedoStack"/> captured by <see cref="UndoRedoStack.CapturePosition"/>.</summary>
/// <param name="Depth">The number of recorded commands.</param>
/// <param name="Top">The newest recorded command, or null.</param>
/// <param name="Epoch">How many times the saved state was forgotten before the capture.</param>
public readonly record struct SavePoint(int Depth, IUndoableCommand? Top, int Epoch);

/// <summary>
/// Undo/redo history of one class editor (the WPF editor used a global singleton).
/// </summary>
public sealed class UndoRedoStack
{
    private readonly Stack<IUndoableCommand> undoStack = new();
    private readonly Stack<IUndoableCommand> redoStack = new();
    private int? savedDepth;
    private IUndoableCommand? savedTop;
    private int epoch;
    private int applying;

    /// <summary>Whether <see cref="Undo"/> would undo a command.</summary>
    public bool CanUndo => undoStack.Count > 0;

    /// <summary>Whether <see cref="Redo"/> would redo a command.</summary>
    public bool CanRedo => redoStack.Count > 0;

    /// <summary>Whether the history is exactly where <see cref="MarkSaved()"/> last recorded it (same depth, same top command).</summary>
    public bool IsAtSavedState => savedDepth is { } depth && depth == undoStack.Count && ReferenceEquals(savedTop, TopOrNull());

    /// <summary>Whether a command is being executed, undone or redone right now: a model change seen meanwhile belongs to the history, not to an edit that bypasses it.</summary>
    public bool IsApplying => applying > 0;

    /// <summary>The <see cref="IUndoableCommand.Name"/> of the command <see cref="Undo"/> would undo, or null.</summary>
    public string? UndoName => undoStack.TryPeek(out var command) ? command.Name : null;

    /// <summary>The <see cref="IUndoableCommand.Name"/> of the command <see cref="Redo"/> would redo, or null.</summary>
    public string? RedoName => redoStack.TryPeek(out var command) ? command.Name : null;

    /// <summary>Records the current history position as the saved state and raises <see cref="Changed"/>.</summary>
    public void MarkSaved()
    {
        savedDepth = undoStack.Count;
        savedTop = TopOrNull();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Captures the current history position so a save that starts now can mark exactly that state as saved with <see cref="MarkSaved(SavePoint)"/>.</summary>
    /// <returns>The position, valid until <see cref="ForgetSavedState"/> or <see cref="Clear"/> is called.</returns>
    public SavePoint CapturePosition() => new(undoStack.Count, TopOrNull(), epoch);

    /// <summary>Records <paramref name="point"/> as the saved state, unless <see cref="ForgetSavedState"/> or <see cref="Clear"/> ran since it was captured.</summary>
    /// <param name="point">A position returned by <see cref="CapturePosition"/>.</param>
    public void MarkSaved(SavePoint point)
    {
        if (point.Epoch != epoch)
        {
            return;
        }

        savedDepth = point.Depth;
        savedTop = point.Top;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Forgets the saved state: a change that bypasses the history (a node delete) calls this, so returning to the old position does not count as saved.</summary>
    public void ForgetSavedState()
    {
        epoch++;
        if (savedDepth is null)
        {
            return;
        }

        savedDepth = null;
        savedTop = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Raised after <see cref="Do"/>, <see cref="Undo"/>, <see cref="Redo"/> or <see cref="Clear"/> changes the history.</summary>
    public event EventHandler? Changed;

    /// <summary>
    /// Raised after <see cref="Do"/>, <see cref="Undo"/> or <see cref="Redo"/> applies a command to
    /// the model, not after <see cref="Clear"/> (editor-services.md §3): a class editor marks its
    /// class dirty on this event, since <see cref="Clear"/> itself changes no model state.
    /// </summary>
    public event EventHandler? Applied;

    /// <summary>Executes a command and records it. Clears the redo history (PAR-60).</summary>
    public void Do(IUndoableCommand command)
    {
        RunApplying(command.Execute);
        Record(command);
    }

    /// <summary>Records a command whose <see cref="IUndoableCommand.Execute"/> the caller already ran, so a command that turned out to change nothing can be left out. Clears the redo history.</summary>
    /// <param name="command">The executed command.</param>
    public void Record(IUndoableCommand command)
    {
        undoStack.Push(command);
        redoStack.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
        Applied?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Undoes the last command. Returns whether a command was undone.</summary>
    public bool Undo()
    {
        if (!undoStack.TryPop(out var command))
        {
            return false;
        }

        RunApplying(command.Undo);
        redoStack.Push(command);
        Changed?.Invoke(this, EventArgs.Empty);
        Applied?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>Redoes the last undone command. Returns whether a command was redone.</summary>
    public bool Redo()
    {
        if (!redoStack.TryPop(out var command))
        {
            return false;
        }

        RunApplying(command.Execute);
        undoStack.Push(command);
        Changed?.Invoke(this, EventArgs.Empty);
        Applied?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>Clears the undo and redo history without executing or undoing anything.</summary>
    public void Clear()
    {
        undoStack.Clear();
        redoStack.Clear();
        epoch++;
        savedDepth = null;
        savedTop = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Runs <paramref name="action"/> with <see cref="IsApplying"/> true; for a caller that executes a command itself before <see cref="Record"/>.</summary>
    /// <param name="action">The action that changes the model.</param>
    public void RunApplying(Action action)
    {
        applying++;
        try
        {
            action();
        }
        finally
        {
            applying--;
        }
    }

    private IUndoableCommand? TopOrNull() => undoStack.TryPeek(out var command) ? command : null;
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
