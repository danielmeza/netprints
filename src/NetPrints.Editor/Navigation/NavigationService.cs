using System.ComponentModel;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Navigation;

/// <summary>
/// The navigation history of one shell window. It records the active graph's view before go-to, an error and a connection
/// jump (through <see cref="NavigateTo"/> or <see cref="RecordCurrent"/>) and when the active tab changes, and restores
/// entries on Back and Forward.
/// </summary>
public sealed class NavigationService : INavigation, IDisposable
{
    private readonly ShellViewModel shell;
    private readonly IShell api;
    private readonly NavigationHistory history = new();
    private GraphDocumentViewModel? tracked;
    private int suppressed;

    /// <summary>Creates the service over a shell.</summary>
    /// <param name="shell">The shell state: the session, the open documents and the active one.</param>
    /// <param name="api">Opens documents.</param>
    public NavigationService(ShellViewModel shell, IShell api)
    {
        ArgumentNullException.ThrowIfNull(shell);
        ArgumentNullException.ThrowIfNull(api);
        this.shell = shell;
        this.api = api;
        tracked = shell.ActiveDocument as GraphDocumentViewModel;
        shell.PropertyChanged += OnShellChanged;
    }

    /// <inheritdoc/>
    public bool CanGoBack => history.CanGoBack(Exists);

    /// <inheritdoc/>
    public bool CanGoForward => history.CanGoForward(Exists);

    /// <inheritdoc/>
    public bool NavigateTo(NavigationTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (!Exists(target.Document))
        {
            return false;
        }

        RecordCurrent();
        Open(target.Document);
        if (target.NodeId is { } nodeId && shell.FindDocument(target.Document) is GraphDocumentViewModel document)
        {
            document.Graph.RevealNode(nodeId);
        }

        return true;
    }

    /// <inheritdoc/>
    public void RecordCurrent()
    {
        if (Capture(shell.ActiveDocument as GraphDocumentViewModel) is { } entry)
        {
            history.Record(entry);
        }
    }

    /// <inheritdoc/>
    public void GoBack()
    {
        if (history.Back(Capture(shell.ActiveDocument as GraphDocumentViewModel), Exists) is { } entry)
        {
            Restore(entry);
        }
    }

    /// <inheritdoc/>
    public void GoForward()
    {
        if (history.Forward(Capture(shell.ActiveDocument as GraphDocumentViewModel), Exists) is { } entry)
        {
            Restore(entry);
        }
    }

    /// <summary>Stops following the shell.</summary>
    public void Dispose() => shell.PropertyChanged -= OnShellChanged;

    private static NavigationEntry? Capture(GraphDocumentViewModel? document) =>
        document is null
            ? null
            : new NavigationEntry(document.Id, document.ViewportLocation, document.ViewportZoom, [.. document.Graph.SelectedNodes.Select(node => node.Node.Id)]);

    private bool Exists(DocumentId id) => shell.Session is { } session && CommandTargets.GraphOf(session, id) is not null;

    private void Open(DocumentId id)
    {
        suppressed++;
        try
        {
            api.OpenDocument(id);
        }
        finally
        {
            suppressed--;
        }
    }

    private void Restore(NavigationEntry entry)
    {
        Open(entry.Document);
        if (shell.FindDocument(entry.Document) is not GraphDocumentViewModel document)
        {
            return;
        }

        document.ViewportZoom = entry.Zoom;
        document.ViewportLocation = entry.Location;
        HashSet<string> ids = [.. entry.SelectedNodeIds];
        document.Graph.SelectNodes(document.Graph.Nodes.Where(node => ids.Contains(node.Node.Id)), deselectPrevious: true);
    }

    private void OnShellChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ShellViewModel.ActiveDocument))
        {
            return;
        }

        GraphDocumentViewModel? left = tracked;
        tracked = shell.ActiveDocument as GraphDocumentViewModel;
        if (suppressed == 0 && left is not null && !ReferenceEquals(left, tracked) && Capture(left) is { } entry)
        {
            history.Record(entry);
        }
    }
}
