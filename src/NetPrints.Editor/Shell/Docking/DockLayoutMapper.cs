using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm.Controls;

namespace NetPrints.Editor.Shell.Docking;

/// <summary>The layout Dock should hold, built from a persisted one, before any of it is shown.</summary>
/// <param name="Root">The main window's layout.</param>
/// <param name="Hidden">The hidden panels.</param>
/// <param name="Windows">The floating windows to open once the layout is attached.</param>
internal sealed record BuiltLayout(IRootDock Root, IReadOnlyList<ShellTool> Hidden, IReadOnlyList<(IDockable Dock, FloatingWindowDto Bounds)> Windows);

/// <summary>Maps Dock's model to and from the NetPrints-owned DTO tree (ADR-0018). Dock's own serializers are never used.</summary>
internal static class DockLayoutMapper
{
    private const string RootId = "netprints.root";
    private const double DefaultWindowWidth = 300;
    private const double DefaultWindowHeight = 400;

    /// <summary>Describes a layout.</summary>
    /// <param name="layout">The main layout.</param>
    /// <param name="activeDocument">The active document's id, or null.</param>
    /// <returns>The persisted form.</returns>
    public static DockLayoutDto Capture(IRootDock layout, string? activeDocument)
    {
        DockNodeDto root = layout.VisibleDockables?.Select(CaptureNode).OfType<DockNodeDto>().FirstOrDefault()
            ?? throw new InvalidOperationException("The layout has no main window content.");
        List<FloatingWindowDto> windows = [];
        foreach (IDockWindow window in layout.Windows ?? [])
        {
            window.Save();
            if (window.Layout?.VisibleDockables?.Select(CaptureNode).OfType<DockNodeDto>().FirstOrDefault() is { } content)
            {
                windows.Add(new FloatingWindowDto(Finite(window.X, 0), Finite(window.Y, 0), Finite(window.Width, DefaultWindowWidth), Finite(window.Height, DefaultWindowHeight), content));
            }
        }

        string[] hidden = [.. (layout.HiddenDockables ?? []).OfType<ShellTool>().Select(tool => tool.Id)];
        return new DockLayoutDto(root, hidden, windows, activeDocument);
    }

    /// <summary>Builds a layout from its persisted form; an unknown panel or document is dropped.</summary>
    /// <param name="factory">The factory that creates the docks.</param>
    /// <param name="panels">The registered panels.</param>
    /// <param name="layout">The persisted layout.</param>
    /// <param name="document">Gives the tab of a document id, or null when the document cannot be opened.</param>
    /// <returns>The layout.</returns>
    /// <exception cref="InvalidOperationException">The layout names a kind of node this editor does not know.</exception>
    public static BuiltLayout Build(ShellDockFactory factory, IReadOnlyList<PanelViewModel> panels, DockLayoutDto layout, Func<string, ShellDocument?> document)
    {
        var builder = new Builder(factory, panels, document);
        IDockable main = builder.Node(layout.Root) ?? throw new InvalidOperationException("The layout has no main window content.");
        IRootDock root = factory.CreateRootDock();
        root.Id = RootId;
        root.VisibleDockables = factory.CreateList(main);
        root.ActiveDockable = main;
        root.DefaultDockable = main;

        List<ShellTool> hidden = [];
        foreach (string id in layout.Hidden ?? [])
        {
            if (builder.Tool(id) is { } tool)
            {
                hidden.Add(tool);
            }
        }

        List<(IDockable, FloatingWindowDto)> windows = [];
        foreach (FloatingWindowDto window in layout.Windows ?? [])
        {
            if (builder.Node(window.Root) is { } content && HasContent(content))
            {
                windows.Add((content, window));
            }
        }

        return new BuiltLayout(root, hidden, windows);
    }

    private static DockNodeDto? CaptureNode(IDockable dockable) => dockable switch
    {
        IProportionalDock dock => Node(DockNodeKinds.Proportional, dock, children: Children(dock), orientation: dock.Orientation.ToString().ToLowerInvariant()),
        IToolDock dock => Node(DockNodeKinds.Tools, dock, children: Children(dock), activeId: dock.ActiveDockable?.Id, alignment: dock.Alignment.ToString()),
        IDocumentDock dock => Node(DockNodeKinds.Documents, dock, children: Children(dock), activeId: dock.ActiveDockable?.Id),
        IProportionalDockSplitter => new DockNodeDto(DockNodeKinds.Splitter, dockable.Id),
        ShellTool => new DockNodeDto(DockNodeKinds.Tool, dockable.Id),
        ShellDocument => new DockNodeDto(DockNodeKinds.Document, dockable.Id),
        _ => null,
    };

    private static DockNodeDto Node(string kind, IDockable dockable, IReadOnlyList<DockNodeDto> children, string? orientation = null, string? alignment = null, string? activeId = null) =>
        new(kind, dockable.Id, double.IsFinite(dockable.Proportion) ? dockable.Proportion : null, orientation, alignment, activeId, children);

    private static List<DockNodeDto> Children(IDock dock) => [.. (dock.VisibleDockables ?? []).Select(CaptureNode).OfType<DockNodeDto>()];

    private static double Finite(double value, double fallback) => double.IsFinite(value) ? value : fallback;

    private static bool HasContent(IDockable dockable) =>
        dockable is ShellTool or ShellDocument || (dockable is IDock dock && (dock.VisibleDockables ?? []).Any(HasContent));

    private sealed class Builder(ShellDockFactory factory, IReadOnlyList<PanelViewModel> panels, Func<string, ShellDocument?> document)
    {
        private readonly HashSet<string> usedTools = [];

        public ShellTool? Tool(string id) =>
            panels.FirstOrDefault(panel => string.Equals(panel.Id, id, StringComparison.Ordinal)) is { } panel && usedTools.Add(id) ? ShellDockFactory.NewTool(panel) : null;

        public IDockable? Node(DockNodeDto node) => node.Kind switch
        {
            DockNodeKinds.Proportional => Proportional(node),
            DockNodeKinds.Tools => Tools(node),
            DockNodeKinds.Documents => Documents(node),
            DockNodeKinds.Splitter => new ProportionalDockSplitter { Id = node.Id },
            DockNodeKinds.Tool => Tool(node.Id),
            DockNodeKinds.Document => document(node.Id),
            _ => throw new InvalidOperationException($"Unknown dock node kind '{node.Kind}'."),
        };

        private IDockable Proportional(DockNodeDto node)
        {
            IProportionalDock dock = factory.CreateProportionalDock();
            Common(dock, node);
            dock.Orientation = node.Orientation switch
            {
                "horizontal" => Orientation.Horizontal,
                "vertical" => Orientation.Vertical,
                _ => throw new InvalidOperationException($"Unknown orientation '{node.Orientation}'."),
            };
            dock.VisibleDockables = factory.CreateList(Children(node));
            return dock;
        }

        private IDockable Tools(DockNodeDto node)
        {
            IToolDock dock = factory.CreateToolDock();
            Common(dock, node);
            dock.Alignment = Enum.TryParse(node.Alignment, ignoreCase: true, out Alignment alignment) ? alignment : Alignment.Unset;
            dock.IsCollapsable = false;
            IDockable[] children = Children(node);
            dock.VisibleDockables = factory.CreateList(children);
            dock.ActiveDockable = children.FirstOrDefault(child => child.Id == node.ActiveId) ?? children.FirstOrDefault();
            return dock;
        }

        private IDockable Documents(DockNodeDto node)
        {
            IDocumentDock dock = factory.CreateDocumentDock();
            Common(dock, node);
            dock.CanCreateDocument = false;
            dock.IsCollapsable = false;
            IDockable[] children = Children(node);
            dock.VisibleDockables = factory.CreateList(children);
            dock.ActiveDockable = children.FirstOrDefault(child => child.Id == node.ActiveId) ?? children.FirstOrDefault();
            return dock;
        }

        private static void Common(IDock dock, DockNodeDto node)
        {
            dock.Id = node.Id;
            if (node.Proportion is { } proportion && double.IsFinite(proportion))
            {
                dock.Proportion = proportion;
            }
        }

        private IDockable[] Children(DockNodeDto node) => [.. (node.Children ?? []).Select(Node).OfType<IDockable>()];
    }
}
