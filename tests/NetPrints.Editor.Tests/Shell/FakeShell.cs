using NetPrints.Editor.Contributions;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Navigation;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Tests.Shell;

/// <summary>An <see cref="IShell"/> that records every call, for command handler tests.</summary>
public sealed class FakeShell : IShell
{
    private readonly List<DocumentId> open = [];

    /// <inheritdoc/>
    public IProjectActions ProjectActions => Project;

    /// <inheritdoc/>
    INavigation IShell.Navigation => Navigation;

    /// <summary>Gets the recording navigation.</summary>
    public FakeNavigation Navigation { get; } = new();

    /// <summary>Gets the recording project actions.</summary>
    public FakeProjectActions Project { get; } = new();

    /// <summary>Gets the calls made, in order, as <c>Method:argument</c>.</summary>
    public List<string> Calls { get; } = [];

    /// <summary>Gets the open documents in opening order.</summary>
    public IReadOnlyList<DocumentId> OpenDocuments => open;

    /// <summary>Gets the ids of the panels currently shown.</summary>
    public HashSet<string> VisiblePanels { get; } = [];

    /// <summary>Gets the floating documents.</summary>
    public HashSet<DocumentId> Floating { get; } = [];

    /// <summary>Gets the documents that cannot be opened: <see cref="OpenDocument"/> is called for them and opens nothing.</summary>
    public HashSet<DocumentId> Unresolvable { get; } = [];

    /// <inheritdoc/>
    public DocumentId? ActiveDocument { get; private set; }

    /// <summary>Gets or sets what runs after <see cref="OpenDocument"/> opened or activated a document, in place of the layout creating its view model.</summary>
    public Action<DocumentId>? Opened { get; set; }

    /// <inheritdoc/>
    public bool IsPanelVisible(string panelId) => VisiblePanels.Contains(panelId);

    /// <inheritdoc/>
    public bool IsFloating(DocumentId id) => Floating.Contains(id);

    /// <inheritdoc/>
    public void OpenDocument(DocumentId id)
    {
        Calls.Add($"OpenDocument:{id}");
        if (Unresolvable.Contains(id))
        {
            return;
        }

        if (!open.Contains(id))
        {
            open.Add(id);
        }

        ActiveDocument = id;
        Opened?.Invoke(id);
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

    /// <summary>Builds a context over this shell; the active document is the shell's.</summary>
    /// <param name="parameter">The command parameter, or null.</param>
    /// <param name="session">The open project session, or null for the start page.</param>
    /// <param name="graph">The active graph view model, or null.</param>
    /// <param name="selection">The selection; nothing selected when null.</param>
    /// <param name="scope">The scope the invocation comes from.</param>
    /// <returns>The context.</returns>
    public CommandContext Context(object? parameter = null, ProjectSessionViewModel? session = null, NodeGraphViewModel? graph = null, CommandSelection? selection = null, CommandScope scope = CommandScope.Global) =>
        new(this, session, ActiveDocument, graph, selection ?? CommandSelection.None, parameter, scope);
}
