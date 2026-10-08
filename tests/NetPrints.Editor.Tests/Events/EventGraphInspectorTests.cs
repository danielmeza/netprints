using NetPrints.Core;
using NetPrints.Editor.Events;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Graph.Nodes;
using NetPrints.Editor.Navigation;
using NetPrints.Editor.ProjectTree;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Tests.ProjectTree;
using NetPrints.Graph;
using DocumentId = NetPrints.Editor.Shell.DocumentId;

namespace NetPrints.Editor.Tests.Events;

/// <summary>US8 (FR-070, FR-071): the event graph inspector with rename, and the entry inspector the panel shows for a selected entry.</summary>
public sealed class EventGraphInspectorTests : IAsyncDisposable
{
    private readonly ShellPanelRig rig = new();
    private readonly List<NodeGraphViewModel> graphs = [];

    public ValueTask DisposeAsync()
    {
        graphs.ForEach(graph => graph.Dispose());
        return rig.DisposeAsync();
    }

    private async Task<(ProjectSessionViewModel Session, ClassGraph Class, EventGraph Graph)> OpenWithGraphAsync(params string[] names)
    {
        ProjectSessionViewModel session = await rig.OpenSessionAsync();
        ClassGraph cls = session.Project.Classes[0];
        foreach (string name in names)
        {
            cls.EventGraphs.Add(new EventGraph(name) { Class = cls });
        }

        return (session, cls, cls.EventGraphs[0]);
    }

    [Fact]
    public async Task ASelectedEventGraphShowsItsInspector()
    {
        var (session, cls, graph) = await OpenWithGraphAsync("Ticks");

        rig.Tree.SelectedItem = rig.Item(TreeItemKind.EventGraph, "Ticks");

        EventGraphViewModel inspector = Assert.IsType<EventGraphViewModel>(rig.Inspector.Content);
        Assert.Same(graph, inspector.Graph);
        Assert.Equal("Ticks", inspector.Name);
        Assert.Same(session.ContextFor(cls).EventGraphs.Single(), inspector);
    }

    [Fact]
    public async Task RenamingInTheInspectorUpdatesTheTreeAndTheBreadcrumbsInOneUndoStep()
    {
        var (session, cls, graph) = await OpenWithGraphAsync("Ticks");
        var view = new NodeGraphViewModel(graph, session.ContextFor(cls).Services);
        graphs.Add(view);
        using var breadcrumbs = new BreadcrumbsViewModel(session.Project, view, rig.Shell, _ => true);
        rig.Tree.SelectedItem = rig.Item(TreeItemKind.EventGraph, "Ticks");
        EventGraphViewModel inspector = Assert.IsType<EventGraphViewModel>(rig.Inspector.Content);

        inspector.Name = "Gameplay";

        Assert.Equal("Gameplay", graph.Name);
        Assert.Equal("Gameplay", rig.Tree.SelectedItem?.Name);
        Assert.Equal("Gameplay", breadcrumbs.Segments[^1].Name);
        Assert.Equal("Gameplay", view.Name);
        Assert.Null(inspector.Error);
        Assert.Equal("Rename event graph", session.ContextFor(cls).UndoRedo.UndoName);

        session.ContextFor(cls).UndoRedo.Undo();

        Assert.Equal("Ticks", graph.Name);
        Assert.Equal("Ticks", inspector.Name);
        Assert.Equal("Ticks", rig.Tree.SelectedItem?.Name);
        Assert.Equal("Ticks", breadcrumbs.Segments[^1].Name);
    }

    [Fact]
    public async Task ADuplicateNameIsRefusedWithTheMessageAndTheNameStays()
    {
        var (session, cls, graph) = await OpenWithGraphAsync("Ticks", "Other");
        rig.Tree.SelectedItem = rig.Item(TreeItemKind.EventGraph, "Ticks");
        EventGraphViewModel inspector = Assert.IsType<EventGraphViewModel>(rig.Inspector.Content);

        inspector.Name = "Other";

        Assert.Equal("An event graph named 'Other' already exists", inspector.Error);
        Assert.Equal("Ticks", graph.Name);
        Assert.Equal("Ticks", inspector.Name);
        Assert.False(session.ContextFor(cls).UndoRedo.CanUndo);

        inspector.Name = "Fresh";

        Assert.Null(inspector.Error);
    }

    [Fact]
    public async Task ASelectedEntryOnTheCanvasShowsTheEntryInspector()
    {
        var (session, cls, graph) = await OpenWithGraphAsync("Ticks");
        var entry = new EventEntryNode(graph, "OnTick");
        entry.SetArguments([new EventArgument("dt", TypeSpecifier.FromType<float>())]);
        var view = new NodeGraphViewModel(graph, session.ContextFor(cls).Services);
        graphs.Add(view);
        var document = new GraphDocumentViewModel(DocumentId.Graph(session.ClassPathOf(cls), DocumentId.EventKeyPrefix + graph.Id), view, cls, session);
        rig.Shell.ActiveDocument = rig.Shell.AddDocument(document);

        foreach (NodeViewModel item in view.Nodes)
        {
            item.IsSelected = ReferenceEquals(item.Node, entry);
        }

        EventEntryInspectorViewModel inspector = Assert.IsType<EventEntryInspectorViewModel>(rig.Inspector.Content);
        Assert.Equal("OnTick", inspector.Name);
        Assert.Equal(["dt"], inspector.Arguments.Select(argument => argument.Name));
        Assert.Equal("OnTick", rig.Shell.TreeSelection is EventEntryNode node ? node.EventName : null);

        inspector.Name = "OnFrame";
        Assert.Equal("OnFrame", entry.EventName);
    }
}
