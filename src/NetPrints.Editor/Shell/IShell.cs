using NetPrints.Editor.Navigation;

namespace NetPrints.Editor.Shell;

/// <summary>The only shell API features use: documents, panels and layout (ADR-0018 adapter implements it).</summary>
public interface IShell
{
    /// <summary>Gets the project-level flows the project commands call.</summary>
    IProjectActions ProjectActions { get; }

    /// <summary>Gets the navigation history and go-to services.</summary>
    INavigation Navigation { get; }

    /// <summary>Gets the active document, or null when none is open.</summary>
    DocumentId? ActiveDocument { get; }

    /// <summary>Gets the open documents, docked and floating, in tab order.</summary>
    IReadOnlyList<DocumentId> OpenDocuments { get; }

    /// <summary>Gets whether a panel is shown: in the layout, docked or floating, and not hidden.</summary>
    /// <param name="panelId">The panel id.</param>
    /// <returns><see langword="true"/> when the panel is shown.</returns>
    bool IsPanelVisible(string panelId);

    /// <summary>Gets whether an open document is in a window of its own.</summary>
    /// <param name="id">The document.</param>
    /// <returns><see langword="true"/> when the document is floating.</returns>
    bool IsFloating(DocumentId id);

    /// <summary>Opens a document and activates it; an already open one is just activated.</summary>
    /// <param name="id">The document.</param>
    void OpenDocument(DocumentId id);

    /// <summary>Activates an open document.</summary>
    /// <param name="id">The document.</param>
    void ActivateDocument(DocumentId id);

    /// <summary>Closes a document.</summary>
    /// <param name="id">The document.</param>
    void CloseDocument(DocumentId id);

    /// <summary>Shows a panel.</summary>
    /// <param name="panelId">The panel id.</param>
    void ShowPanel(string panelId);

    /// <summary>Hides a panel.</summary>
    /// <param name="panelId">The panel id.</param>
    void HidePanel(string panelId);

    /// <summary>Moves a document into its own window.</summary>
    /// <param name="id">The document.</param>
    void FloatDocument(DocumentId id);

    /// <summary>Moves a floating document back into the main window.</summary>
    /// <param name="id">The document.</param>
    void DockDocument(DocumentId id);

    /// <summary>Restores the default layout.</summary>
    void ResetLayout();
}
