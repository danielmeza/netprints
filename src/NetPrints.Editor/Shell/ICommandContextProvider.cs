namespace NetPrints.Editor.Shell;

/// <summary>Builds the <see cref="CommandContext"/> of one command invocation from the shell's current state.</summary>
public interface ICommandContextProvider
{
    /// <summary>Builds a context for an invocation.</summary>
    /// <param name="parameter">The command parameter, or null.</param>
    /// <returns>A fresh context.</returns>
    CommandContext Create(object? parameter = null);

    /// <summary>
    /// Raised, on the UI thread, when anything a handler's <c>CanExecute</c> or <c>DynamicLabel</c> reads may have
    /// changed: the session, the undo history, the selection, the active document or the building and running state.
    /// A visible surface re-queries its commands on it.
    /// </summary>
    event EventHandler? CommandStatesChanged;
}
