namespace NetPrints.Editor.Shell;

/// <summary>A message the session asks the shell to show in the status bar.</summary>
/// <param name="Text">The message.</param>
/// <param name="Expiry">How long it stays, or null to keep it until replaced.</param>
public sealed record SessionStatus(string Text, TimeSpan? Expiry = null)
{
    /// <summary>How long a message about a finished action (a save, an undo or a redo) stays.</summary>
    public static readonly TimeSpan TransientLifetime = TimeSpan.FromSeconds(4);
}
