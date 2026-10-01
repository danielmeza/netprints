using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Tests.Shell;

/// <summary>An <see cref="IShell"/> that records every call, for command handler tests.</summary>
public sealed class FakeShell : IShell
{
    private readonly List<DocumentId> open = [];

    /// <summary>Gets the calls made, in order, as <c>Method:argument</c>.</summary>
    public List<string> Calls { get; } = [];

    /// <summary>Gets the open documents in opening order.</summary>
    public IReadOnlyList<DocumentId> OpenDocuments => open;

    /// <summary>Gets the ids of the panels currently shown.</summary>
    public HashSet<string> VisiblePanels { get; } = [];

    /// <summary>Gets the floating documents.</summary>
    public HashSet<DocumentId> Floating { get; } = [];

    /// <inheritdoc/>
    public DocumentId? ActiveDocument { get; private set; }

    /// <inheritdoc/>
    public void OpenDocument(DocumentId id)
    {
        Calls.Add($"OpenDocument:{id}");
        if (!open.Contains(id))
        {
            open.Add(id);
        }

        ActiveDocument = id;
    }

    /// <inheritdoc/>
    public void ActivateDocument(DocumentId id)
    {
        Calls.Add($"ActivateDocument:{id}");
        if (open.Contains(id))
        {
            ActiveDocument = id;
        }
    }

    /// <inheritdoc/>
    public void CloseDocument(DocumentId id)
    {
        Calls.Add($"CloseDocument:{id}");
        open.Remove(id);
        Floating.Remove(id);
        if (ActiveDocument == id)
        {
            ActiveDocument = open.Count > 0 ? open[^1] : null;
        }
    }

    /// <inheritdoc/>
    public void ShowPanel(string panelId)
    {
        Calls.Add($"ShowPanel:{panelId}");
        VisiblePanels.Add(panelId);
    }

    /// <inheritdoc/>
    public void HidePanel(string panelId)
    {
        Calls.Add($"HidePanel:{panelId}");
        VisiblePanels.Remove(panelId);
    }

    /// <inheritdoc/>
    public void FloatDocument(DocumentId id)
    {
        Calls.Add($"FloatDocument:{id}");
        Floating.Add(id);
    }

    /// <inheritdoc/>
    public void DockDocument(DocumentId id)
    {
        Calls.Add($"DockDocument:{id}");
        Floating.Remove(id);
    }

    /// <inheritdoc/>
    public void ResetLayout()
    {
        Calls.Add("ResetLayout");
        Floating.Clear();
    }

    /// <summary>Builds a context over this shell with nothing selected.</summary>
    /// <param name="parameter">The command parameter, or null.</param>
    /// <param name="session">The open project session, or null for the start page.</param>
    /// <returns>The context.</returns>
    public CommandContext Context(object? parameter = null, ProjectSessionViewModel? session = null) =>
        new(this, session, ActiveDocument, null, CommandSelection.None, parameter);
}
