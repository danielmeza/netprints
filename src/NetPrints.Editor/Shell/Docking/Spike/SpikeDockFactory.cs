using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;
using NetPrints.Compilation;
using NetPrints.Editor.ErrorList;

namespace NetPrints.Editor.Shell.Docking.Spike;

/// <summary>Builds the spike layout: a tool pane, a splitter and a document dock with two tabs.</summary>
internal sealed class SpikeDockFactory : Factory
{
    /// <summary>Id of the docked tool pane.</summary>
    public const string ToolId = "netprints.panel.spike";

    /// <summary>Id of the first (initially active) document.</summary>
    public static readonly string FirstDocumentId = DocumentId.Graph("A.cs", "method:1").ToString();

    /// <summary>Id of the second document, the one the spike floats.</summary>
    public static readonly string SecondDocumentId = DocumentId.Graph("A.cs", "method:2").ToString();

    private const string RootId = "netprints.root";
    private const string BodyId = "netprints.body";
    /// <summary>Id of the document dock in the main window.</summary>
    public const string DocumentsId = "netprints.documents";


    private const string ToolsId = "netprints.tools";
    private const string SplitterId = "netprints.splitter";
    private const double ToolProportion = 0.3;

    /// <summary>The NetPrints view models the spike documents show, by dockable id; null for an id the app no longer knows.</summary>
    /// <param name="id">The dockable id.</param>
    /// <returns>The view model, or null.</returns>
    public static object? Resolve(string id) => id switch
    {
        ToolId => Row("NPT-TOOL", "Tool pane"),
        _ when id == FirstDocumentId => Row("NPT-A", "First document"),
        _ when id == SecondDocumentId => Row("NPT-B", "Second document"),
        _ => null,
    };

    /// <inheritdoc/>
    public override IRootDock CreateLayout()
    {
        var first = new SpikeDocument { Id = FirstDocumentId, Title = "A#1", Context = Resolve(FirstDocumentId) };
        var second = new SpikeDocument { Id = SecondDocumentId, Title = "A#2", Context = Resolve(SecondDocumentId) };
        var tool = new SpikeTool { Id = ToolId, Title = "Spike tool", Context = Resolve(ToolId) };

        var documents = new DocumentDock
        {
            Id = DocumentsId,
            Proportion = 1 - ToolProportion,
            VisibleDockables = CreateList<IDockable>(first, second),
            ActiveDockable = first,
        };
        var tools = new ToolDock
        {
            Id = ToolsId,
            Proportion = ToolProportion,
            Alignment = Alignment.Left,
            VisibleDockables = CreateList<IDockable>(tool),
            ActiveDockable = tool,
        };
        var body = new ProportionalDock
        {
            Id = BodyId,
            Orientation = Orientation.Horizontal,
            VisibleDockables = CreateList<IDockable>(tools, new ProportionalDockSplitter { Id = SplitterId }, documents),
        };
        var root = new RootDock
        {
            Id = RootId,
            VisibleDockables = CreateList<IDockable>(body),
            ActiveDockable = body,
            DefaultDockable = body,
        };
        return root;
    }

    /// <summary>Re-attaches the NetPrints view model to every spike dockable of a loaded layout by its id, and drops the ones the app no longer knows.</summary>
    /// <param name="layout">The loaded layout.</param>
    /// <param name="resolve">Maps a dockable id to its view model, or null when it no longer exists.</param>
    public void Reattach(IRootDock layout, Func<string, object?> resolve)
    {
        foreach (IDockable dockable in Walk(layout).ToList())
        {
            if (dockable is not (SpikeDocument or SpikeTool))
            {
                continue;
            }

            if (resolve(dockable.Id) is { } model)
            {
                dockable.Context = model;
            }
            else
            {
                RemoveDockable(dockable, collapse: true);
            }
        }
    }

    /// <summary>Every dockable under a layout, including the ones in floating windows.</summary>
    /// <param name="layout">The root.</param>
    /// <returns>The dockables, depth first.</returns>
    public static IEnumerable<IDockable> Walk(IRootDock layout)
    {
        foreach (IDockable dockable in WalkDock(layout))
        {
            yield return dockable;
        }

        foreach (IDockWindow window in layout.Windows ?? [])
        {
            if (window.Layout is { } floating)
            {
                foreach (IDockable dockable in Walk(floating))
                {
                    yield return dockable;
                }
            }
        }
    }

    private static IEnumerable<IDockable> WalkDock(IDock dock)
    {
        foreach (IDockable child in dock.VisibleDockables ?? [])
        {
            yield return child;
            if (child is IDock inner)
            {
                foreach (IDockable nested in WalkDock(inner))
                {
                    yield return nested;
                }
            }
        }
    }

    private static DiagnosticRowViewModel Row(string id, string message) =>
        new(new CodeDiagnostic(CodeDiagnosticSeverity.Info, id, message, "A", null, null, null, null), owner: null);
}
