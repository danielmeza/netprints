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

    /// <summary>Opens a new class editor window for the view model.</summary>
    void OpenClassEditor(ClassEditorVM editor);

    /// <summary>Closes the window of a class, if any.</summary>
    void CloseClassEditor(ClassGraph cls);

    /// <summary>Closes every class editor window.</summary>
    void CloseAllClassEditors();
}
