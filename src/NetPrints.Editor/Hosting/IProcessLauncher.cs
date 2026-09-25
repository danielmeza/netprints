namespace NetPrints.Editor.Hosting;

/// <summary>
/// Starts external processes (running compiled projects).
/// </summary>
public interface IProcessLauncher
{
    /// <summary>Starts an external process. Its stdout/stderr are reported through <see cref="OutputReceived"/>.</summary>
    /// <param name="fileName">Executable to start.</param>
    /// <param name="arguments">Command-line arguments, or <see langword="null"/> for none.</param>
    void Start(string fileName, string? arguments);

    /// <summary>
    /// One line of a started process's stdout or stderr, or a status line ("Process exited (code
    /// N).") — for the editor's Output pane. Every started process reports here, on every
    /// platform, whether or not the host has its own visible console for the child.
    /// </summary>
    event Action<string>? OutputReceived;
}
