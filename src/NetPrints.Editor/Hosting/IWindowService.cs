using NetPrints.Core;
using NetPrints.Editor.ClassEditor;

namespace NetPrints.Editor.Hosting;

/// <summary>
/// Class editor windows, keyed by the class they edit (at most one window per class).
/// </summary>
public interface IWindowService
{
    /// <summary>
    /// Brings the window of a class to the front (restoring it when minimized).
    /// Returns false when no window is open for the class.
    /// </summary>
    bool TryActivateClassEditor(ClassGraph cls);

    /// <summary>Creates the class's editor view model and opens its window (this method owns and disposes it).</summary>
    void OpenClassEditor(ClassGraph cls, EditorContext context);

    /// <summary>The open class editor's view model for a class, or <see langword="null"/> if its window is not
    /// open (host <c>focusDocument</c> navigation, R2-21).</summary>
    ClassEditorViewModel? FindClassEditor(ClassGraph cls);

    /// <summary>Closes the window of a class, if any.</summary>
    void CloseClassEditor(ClassGraph cls);

    /// <summary>Closes every class editor window.</summary>
    void CloseAllClassEditors();

    /// <summary>Closes the main window, which ends the application.</summary>
    void CloseMainWindow();
}
