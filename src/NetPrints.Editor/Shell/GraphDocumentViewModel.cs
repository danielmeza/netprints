using CommunityToolkit.Mvvm.ComponentModel;
using NetPrints.Core;
using NetPrints.Editor.Graph;

namespace NetPrints.Editor.Shell;

/// <summary>A graph document: wraps the graph editor's view model and keeps the viewport the canvas shows.</summary>
public sealed partial class GraphDocumentViewModel : DocumentViewModel
{
    private const string UnsavedMark = "*";

    private readonly ClassGraph owner;
    private readonly ProjectSessionViewModel? session;

    /// <summary>Wraps <paramref name="graph"/> as the document <paramref name="id"/>.</summary>
    /// <param name="id">The graph document id.</param>
    /// <param name="graph">The graph editor view model.</param>
    /// <param name="owner">The class that owns the graph; its file is the one that can be unsaved.</param>
    /// <param name="session">The session whose pulses refresh the unsaved state, or null.</param>
    public GraphDocumentViewModel(DocumentId id, NodeGraphViewModel graph, ClassGraph owner, ProjectSessionViewModel? session)
        : base(id, graph.Name)
    {
        Graph = graph;
        this.owner = owner;
        if (session is not null)
        {
            this.session = session;
            session.CommandStatesChanged += OnSessionPulse;
        }

        Refresh();
    }

    /// <summary>Gets the wrapped graph editor view model.</summary>
    public NodeGraphViewModel Graph { get; }

    /// <summary>Gets or sets the invoker the canvas runs the graph-scope key bindings through, or null.</summary>
    [ObservableProperty]
    public partial CommandInvoker? Invoker { get; set; }

    /// <summary>Gets or sets the canvas location in graph units.</summary>
    [ObservableProperty]
    public partial GraphPoint ViewportLocation { get; set; }

    /// <summary>Gets or sets the canvas zoom, 1 being full size.</summary>
    [ObservableProperty]
    public partial double ViewportZoom { get; set; } = 1;

    /// <summary>Re-reads the unsaved state of the owning class file and updates the title.</summary>
    public void Refresh()
    {
        IsUnsaved = owner.IsDirty;
        Title = IsUnsaved ? Graph.Name + UnsavedMark : Graph.Name;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing && session is not null)
        {
            session.CommandStatesChanged -= OnSessionPulse;
        }

        base.Dispose(disposing);
    }

    private void OnSessionPulse(object? sender, EventArgs e) => Refresh();
}
