namespace NetPrints.Editor.State;

/// <summary>
/// Reads and writes the per-user state files (state-files.md §2). A missing, unreadable or newer file reads as the defaults
/// (<see langword="null"/>, or an empty list), with one warning logged, and is not rewritten until the caller saves that state again.
/// Reading never throws. A save that fails is logged and dropped, because losing saved state must never stop the editor.
/// </summary>
public interface IEditorStateStore
{
    /// <summary>Reads <c>window.json</c>.</summary>
    /// <returns>The state, or <see langword="null"/> for the defaults.</returns>
    WindowState? LoadWindow();

    /// <summary>Writes <c>window.json</c>.</summary>
    /// <param name="state">The state to keep.</param>
    void SaveWindow(WindowState state);

    /// <summary>Reads <c>layout.json</c>.</summary>
    /// <returns>The state, or <see langword="null"/> for the default layout.</returns>
    LayoutState? LoadLayout();

    /// <summary>Writes <c>layout.json</c>.</summary>
    /// <param name="state">The state to keep.</param>
    void SaveLayout(LayoutState state);

    /// <summary>Reads <c>recent.json</c>.</summary>
    /// <returns>The state; empty for the defaults.</returns>
    RecentState LoadRecent();

    /// <summary>Writes <c>recent.json</c>.</summary>
    /// <param name="state">The state to keep.</param>
    void SaveRecent(RecentState state);

    /// <summary>Reads the session of a project.</summary>
    /// <param name="projectPath">The project file's path.</param>
    /// <returns>The state, or <see langword="null"/> for none.</returns>
    SessionState? LoadSession(string projectPath);

    /// <summary>Writes the session of a project.</summary>
    /// <param name="state">The state to keep; <see cref="SessionState.ProjectPath"/> names the file.</param>
    void SaveSession(SessionState state);
}
