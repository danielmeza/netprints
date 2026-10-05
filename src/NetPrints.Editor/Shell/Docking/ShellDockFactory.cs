using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Contributions.BuiltIn;

namespace NetPrints.Editor.Shell.Docking;

/// <summary>
/// Builds the shell's default layout (contracts/shell.md section 1) and keeps a floating pane that is closed with the
/// window's own close button from being lost: it docks back to its default place.
/// </summary>
internal sealed class ShellDockFactory : Factory
{
    /// <summary>Id of the main window's document dock.</summary>
    internal const string DocumentsId = "netprints.documents";

    private const string RootId = "netprints.root";
    private const string BodyId = "netprints.body";
    private const string LeftId = "netprints.dock.left";
    private const string RightId = "netprints.dock.right";
    private const string BottomId = "netprints.dock.bottom";
    private const string BodySplitterId = "netprints.splitter.body";
    private const string LeftSplitterId = "netprints.splitter.left";
    private const string RightSplitterId = "netprints.splitter.right";
    private const double LeftProportion = 0.20;
    private const double RightProportion = 0.22;
    private const double BottomProportion = 0.25;

    private readonly IReadOnlyList<PanelViewModel> panels;

    /// <summary>Creates a factory for the shell's panels.</summary>
    /// <param name="panels">The panels, by default dock and order.</param>
    public ShellDockFactory(IReadOnlyList<PanelViewModel> panels)
    {
        this.panels = panels;
        HideToolsOnClose = true;
    }

    /// <summary>Gets or sets the main window's layout, the home of every pane and tab.</summary>
    public IRootDock? MainLayout { get; set; }

    /// <summary>Gets or sets a value indicating whether closing a floating window leaves its panes alone, as while a layout is replaced.</summary>
    public bool SuppressHoming { get; set; }

    /// <summary>Every dockable under a layout that is shown, floating windows included.</summary>
    /// <param name="layout">The main layout.</param>
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
                foreach (IDockable dockable in WalkDock(floating))
                {
                    yield return dockable;
                }
            }
        }
    }

    /// <summary>Whether a dockable sits in a floating window.</summary>
    /// <param name="layout">The main layout.</param>
    /// <param name="dockable">The dockable.</param>
    /// <returns><see langword="true"/> when it is under one of the layout's windows.</returns>
    public static bool IsFloating(IRootDock layout, IDockable dockable) => FloatingWindowOf(layout, dockable) is not null;

    /// <summary>The floating window a dockable sits in.</summary>
    /// <param name="layout">The main layout.</param>
    /// <param name="dockable">The dockable.</param>
    /// <returns>The window, or <see langword="null"/> when the dockable is not floating.</returns>
    public static IDockWindow? FloatingWindowOf(IRootDock layout, IDockable dockable) =>
        (layout.Windows ?? []).FirstOrDefault(window => window.Layout is { } floating && WalkDock(floating).Contains(dockable));

    /// <inheritdoc/>
    public override IDocumentDock CreateDocumentDock() => new DocumentDock { DockCapabilityPolicy = new DockCapabilityPolicy(), DockCapabilityOverrides = new DockCapabilityOverrides() };

    /// <inheritdoc/>
    public override IToolDock CreateToolDock() => new ToolDock { DockCapabilityPolicy = new DockCapabilityPolicy(), DockCapabilityOverrides = new DockCapabilityOverrides() };

    /// <inheritdoc/>
    public override IProportionalDock CreateProportionalDock() => new ProportionalDock { DockCapabilityPolicy = new DockCapabilityPolicy(), DockCapabilityOverrides = new DockCapabilityOverrides() };

    /// <inheritdoc/>
    public override IRootDock CreateRootDock() => new RootDock { DockCapabilityPolicy = new DockCapabilityPolicy(), DockCapabilityOverrides = new DockCapabilityOverrides() };

    /// <inheritdoc/>
    public override IRootDock CreateLayout()
    {
        ToolDock left = Tools(LeftId, PanelDock.Left, LeftProportion, Alignment.Left);
        ToolDock right = Tools(RightId, PanelDock.Right, RightProportion, Alignment.Right);
        ToolDock bottom = Tools(BottomId, PanelDock.Bottom, BottomProportion, Alignment.Bottom);
        var documents = new DocumentDock
        {
            Id = DocumentsId,
            DockCapabilityPolicy = new DockCapabilityPolicy(),
            DockCapabilityOverrides = new DockCapabilityOverrides(),
            Proportion = 1 - LeftProportion - RightProportion,
            CanCreateDocument = false,
            IsCollapsable = false,
            VisibleDockables = CreateList<IDockable>(),
        };
        var body = new ProportionalDock
        {
            Id = BodyId,
            DockCapabilityPolicy = new DockCapabilityPolicy(),
            DockCapabilityOverrides = new DockCapabilityOverrides(),
            Proportion = 1 - BottomProportion,
            Orientation = Orientation.Horizontal,
            VisibleDockables = CreateList<IDockable>(
                left, new ProportionalDockSplitter { Id = LeftSplitterId }, documents, new ProportionalDockSplitter { Id = RightSplitterId }, right),
        };
        var column = new ProportionalDock
        {
            Id = RootId + ".column",
            DockCapabilityPolicy = new DockCapabilityPolicy(),
            DockCapabilityOverrides = new DockCapabilityOverrides(),
            Orientation = Orientation.Vertical,
            VisibleDockables = CreateList<IDockable>(body, new ProportionalDockSplitter { Id = BodySplitterId }, bottom),
        };
        return new RootDock
        {
            Id = RootId,
            DockCapabilityPolicy = new DockCapabilityPolicy(),
            DockCapabilityOverrides = new DockCapabilityOverrides(),
            VisibleDockables = CreateList<IDockable>(column),
            ActiveDockable = column,
            DefaultDockable = column,
        };
    }

    /// <summary>Gets the main window's document dock.</summary>
    /// <returns>The dock, or null when the layout has none.</returns>
    public IDocumentDock? FindDocumentDock() =>
        MainLayout is { } layout ? Walk(layout).OfType<IDocumentDock>().FirstOrDefault(dock => dock.Id == DocumentsId) ?? Walk(layout).OfType<IDocumentDock>().FirstOrDefault() : null;

    /// <summary>Creates the main window's document dock again, between the left and right tool docks, when the layout lost it.</summary>
    /// <returns>The new dock, or null when the layout has no body to put it in.</returns>
    public IDocumentDock? AddDocumentDock()
    {
        if (MainLayout is not { } layout || Walk(layout).OfType<IProportionalDock>().FirstOrDefault(dock => dock.Id == BodyId) is not { } body)
        {
            return null;
        }

        IDocumentDock documents = CreateDocumentDock();
        documents.Id = DocumentsId;
        documents.Proportion = 1 - LeftProportion - RightProportion;
        documents.CanCreateDocument = false;
        documents.IsCollapsable = false;
        documents.VisibleDockables = CreateList<IDockable>();
        int index = (body.VisibleDockables ?? []).TakeWhile(dockable => dockable.Id != RightSplitterId).Count();
        InsertDockable(body, documents, index);
        return documents;
    }

    /// <summary>Puts a panel in its default place: its default tool dock, among the panes there by order.</summary>
    /// <param name="panel">The panel.</param>
    /// <returns>The pane, or null when the layout has no tool dock to put it in.</returns>
    public ShellTool? DockHome(PanelViewModel panel)
    {
        IToolDock? dock = DefaultDockOf(panel.DefaultDock);
        if (dock is null)
        {
            return null;
        }

        ShellTool pane = NewTool(panel);
        int index = (dock.VisibleDockables ?? []).TakeWhile(dockable => dockable is not ShellTool tool || tool.PanelOrder <= panel.Order).Count();
        InsertDockable(dock, pane, index);
        SetActiveDockable(pane);
        return pane;
    }

    /// <inheritdoc/>
    public override void OnWindowClosed(IDockWindow? window)
    {
        base.OnWindowClosed(window);
        if (!SuppressHoming)
        {
            DockOrphanedPanels();
        }
    }

    /// <inheritdoc/>
    public override void OnWindowRemoved(IDockWindow? window)
    {
        base.OnWindowRemoved(window);
        if (!SuppressHoming)
        {
            DockOrphanedPanels();
        }
    }

    /// <summary>
    /// Docks back to its default place every panel that is neither shown nor hidden by the user: one that was left in a
    /// window that has just been closed, which Dock hides into the window's own dock.
    /// </summary>
    public void DockOrphanedPanels()
    {
        if (MainLayout is not { } layout)
        {
            return;
        }

        foreach (IDockable stale in (layout.HiddenDockables ?? []).Where(hidden => hidden is ShellTool && hidden.OriginalOwner is IDock owner && IsFloating(layout, owner)).ToList())
        {
            layout.HiddenDockables?.Remove(stale);
        }

        HashSet<string> known = [.. Walk(layout).OfType<ShellTool>().Select(tool => tool.Id), .. (layout.HiddenDockables ?? []).Select(hidden => hidden.Id)];
        foreach (PanelViewModel panel in panels.Where(panel => !known.Contains(panel.Id)))
        {
            DockHome(panel);
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

    private ToolDock Tools(string id, PanelDock place, double proportion, Alignment alignment)
    {
        ShellTool[] tools = [.. panels.Where(panel => panel.DefaultDock == place).OrderBy(panel => panel.Order).Select(NewTool)];
        return new ToolDock
        {
            Id = id,
            DockCapabilityPolicy = new DockCapabilityPolicy(),
            DockCapabilityOverrides = new DockCapabilityOverrides(),
            Proportion = proportion,
            Alignment = alignment,
            IsCollapsable = false,
            VisibleDockables = CreateList<IDockable>(tools),
            ActiveDockable = tools.FirstOrDefault(tool => tool.Id == PanelContributions.ErrorsId) ?? tools.FirstOrDefault(),
        };
    }

    private static ShellTool NewTool(PanelViewModel panel) => new()
    {
        Id = panel.Id,
        Title = panel.Title,
        Context = panel.Content,
        DefaultDock = panel.DefaultDock,
        PanelOrder = panel.Order,
    };

    private IToolDock? DefaultDockOf(PanelDock place)
    {
        if (MainLayout is not { } layout)
        {
            return null;
        }

        string id = place switch
        {
            PanelDock.Left => LeftId,
            PanelDock.Right => RightId,
            _ => BottomId,
        };
        return Walk(layout).OfType<IToolDock>().FirstOrDefault(dock => dock.Id == id);
    }
}
