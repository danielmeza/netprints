namespace NetPrints.Editor.Shell;

/// <summary>Builds the <see cref="CommandContext"/> of one command invocation from the shell's current state.</summary>
public interface ICommandContextProvider
{
    /// <summary>Builds a context for an invocation.</summary>
    /// <param name="parameter">The command parameter, or null.</param>
    /// <returns>A fresh context.</returns>
    CommandContext Create(object? parameter = null);
}
