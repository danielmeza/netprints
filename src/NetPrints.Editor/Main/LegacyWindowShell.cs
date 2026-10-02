using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Main;

/// <summary>
/// The <see cref="IShell"/> of the class editor windows until the shell replaces them: it has no documents or
/// panels, only the project flows of <see cref="IProjectActions"/>.
/// </summary>
/// <param name="projectActions">The project flows.</param>
internal sealed class LegacyWindowShell(IProjectActions projectActions) : IShell
{
    /// <inheritdoc/>
    public IProjectActions ProjectActions => projectActions;

    /// <inheritdoc/>
    public DocumentId? ActiveDocument => null;

    /// <inheritdoc/>
    public IReadOnlyList<DocumentId> OpenDocuments => [];

    /// <inheritdoc/>
    public bool IsPanelVisible(string panelId) => false;

    /// <inheritdoc/>
    public bool IsFloating(DocumentId id) => false;

    /// <inheritdoc/>
    public void OpenDocument(DocumentId id)
    {
    }

    /// <inheritdoc/>
    public void ActivateDocument(DocumentId id)
    {
    }

    /// <inheritdoc/>
    public void CloseDocument(DocumentId id)
    {
    }

    /// <inheritdoc/>
    public void ShowPanel(string panelId)
    {
    }

    /// <inheritdoc/>
    public void HidePanel(string panelId)
    {
    }

    /// <inheritdoc/>
    public void FloatDocument(DocumentId id)
    {
    }

    /// <inheritdoc/>
    public void DockDocument(DocumentId id)
    {
    }

    /// <inheritdoc/>
    public void ResetLayout()
    {
    }
}
