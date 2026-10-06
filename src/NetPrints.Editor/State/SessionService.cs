using NetPrints.Editor.Graph;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.State;

/// <summary>Saves and restores what one user had open in one project (FR-050, FR-051): the documents in order, the active one and each graph's viewport.</summary>
/// <param name="store">Where the sessions are kept.</param>
internal sealed class SessionService(IEditorStateStore store)
{
    /// <summary>Saves the session of a project; the last writer wins, so with several instances the last one to unload keeps its session.</summary>
    /// <param name="projectPath">The project file's path.</param>
    /// <param name="shell">The shell whose documents are saved.</param>
    /// <param name="find">Finds the view model of an open document.</param>
    public void Save(string projectPath, IShell shell, Func<DocumentId, DocumentViewModel?> find)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectPath);
        ArgumentNullException.ThrowIfNull(shell);
        ArgumentNullException.ThrowIfNull(find);
        IReadOnlyList<DocumentId> open = shell.OpenDocuments;
        Dictionary<string, ViewportState> viewports = [];
        foreach (DocumentId id in open)
        {
            if (find(id) is IViewportDocument document && IsUsable(document.ViewportLocation, document.ViewportZoom))
            {
                viewports[id.ToString()] = new ViewportState(document.ViewportLocation.X, document.ViewportLocation.Y, document.ViewportZoom);
            }
        }

        store.SaveSession(new SessionState(StateFile.CurrentVersion, projectPath, [.. open.Select(id => id.ToString())], shell.ActiveDocument?.ToString(), viewports));
    }

    /// <summary>Opens the documents the project had open and puts each viewport back; whatever cannot be restored is skipped.</summary>
    /// <param name="projectPath">The project file's path.</param>
    /// <param name="shell">The shell that opens the documents.</param>
    /// <param name="find">Finds the view model of an open document.</param>
    public void Restore(string projectPath, IShell shell, Func<DocumentId, DocumentViewModel?> find)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectPath);
        ArgumentNullException.ThrowIfNull(shell);
        ArgumentNullException.ThrowIfNull(find);
        if (store.LoadSession(projectPath) is not { } saved)
        {
            return;
        }

        List<DocumentId> restored = [];
        foreach (string text in saved.OpenDocuments)
        {
            if (!DocumentId.TryParse(text, out DocumentId id) || restored.Contains(id))
            {
                continue;
            }

            shell.OpenDocument(id);
            if (!shell.OpenDocuments.Contains(id))
            {
                continue;
            }

            restored.Add(id);
            if (saved.Viewports.TryGetValue(text, out ViewportState? viewport)
                && viewport is not null
                && IsUsable(new GraphPoint(viewport.X, viewport.Y), viewport.Zoom)
                && find(id) is IViewportDocument document)
            {
                document.ViewportLocation = new GraphPoint(viewport.X, viewport.Y);
                document.ViewportZoom = viewport.Zoom;
            }
        }

        DocumentId? active = DocumentId.TryParse(saved.ActiveDocument, out DocumentId savedActive) && restored.Contains(savedActive) ? savedActive : restored.Count > 0 ? restored[0] : null;
        if (active is { } target)
        {
            shell.ActivateDocument(target);
        }
    }

    private static bool IsUsable(GraphPoint location, double zoom) => double.IsFinite(location.X) && double.IsFinite(location.Y) && double.IsFinite(zoom) && zoom > 0;
}
