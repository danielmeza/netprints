using NetPrints.Core;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Navigation;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Tests.ProjectTree;

namespace NetPrints.Editor.Tests.Navigation;

public sealed class BreadcrumbsViewModelTests : IAsyncDisposable
{
    private readonly ShellPanelRig rig = new();
    private readonly List<NodeGraphViewModel> graphs = [];

    public ValueTask DisposeAsync()
    {
        graphs.ForEach(graph => graph.Dispose());
        return rig.DisposeAsync();
    }

    private async Task<(ProjectSessionViewModel Session, ClassGraph Class, NodeGraphViewModel Graph)> OpenAsync(bool classGraph = false)
    {
        ProjectSessionViewModel session = await rig.OpenSessionAsync();
        ClassGraph cls = session.Project.Classes[0];
        var graph = new NodeGraphViewModel(classGraph ? cls : cls.Methods.First(), session.ContextFor(cls).Services);
        graphs.Add(graph);
        return (session, cls, graph);
    }

    private BreadcrumbsViewModel Create(ProjectSessionViewModel session, NodeGraphViewModel graph, Func<object, bool>? reveal = null) =>
        new(session.Project, graph, rig.Shell, reveal ?? (_ => true));

    [Fact]
    public async Task AMethodGraphShowsProjectClassAndGraph()
    {
        var (session, cls, graph) = await OpenAsync();

        using BreadcrumbsViewModel breadcrumbs = Create(session, graph);

        Assert.Equal([session.Project.Name, cls.Name, graph.Name], breadcrumbs.Segments.Select(segment => segment.Name));
        Assert.Equal([false, false, true], breadcrumbs.Segments.Select(segment => segment.IsLast));
    }

    [Fact]
    public async Task TheClassGraphShowsNoSeparateGraphSegment()
    {
        var (session, cls, graph) = await OpenAsync(classGraph: true);

        using BreadcrumbsViewModel breadcrumbs = Create(session, graph);

        Assert.Equal([session.Project.Name, cls.Name], breadcrumbs.Segments.Select(segment => segment.Name));
    }

    [Fact]
    public async Task RenamesUpdateTheSegments()
    {
        var (session, cls, graph) = await OpenAsync();
        using BreadcrumbsViewModel breadcrumbs = Create(session, graph);

        cls.Name = "Renamed";
        ((MethodGraph)graph.Graph).Name = "Other";
        rig.Shell.NotifyModelRenamed();

        Assert.Equal("Renamed", breadcrumbs.Segments[1].Name);
        Assert.Equal("Other", breadcrumbs.Segments[2].Name);
    }

    [Fact]
    public async Task ChoosingASegmentRevealsItsModel()
    {
        var (session, cls, graph) = await OpenAsync();
        List<object> revealed = [];
        using BreadcrumbsViewModel breadcrumbs = Create(session, graph, model =>
        {
            revealed.Add(model);
            return true;
        });

        breadcrumbs.Segments[0].RevealCommand.Execute(null);
        breadcrumbs.Segments[1].RevealCommand.Execute(null);
        breadcrumbs.Segments[2].RevealCommand.Execute(null);

        Assert.Equal([session.Project, cls, graph.Graph], revealed);
    }

    [Fact]
    public async Task RevealingInTheTreeShowsThePanelAndSelectsTheRow()
    {
        var (session, cls, graph) = await OpenAsync();
        using BreadcrumbsViewModel breadcrumbs = Create(session, graph, BreadcrumbsViewModel.RevealInTree(rig.Shell, rig.Api));

        breadcrumbs.Segments[1].RevealCommand.Execute(null);

        Assert.Contains($"ShowPanel:{PanelContributions.ProjectTreeId}", rig.Api.Calls);
        Assert.Same(cls, rig.Tree.SelectedItem?.Model);
    }

    [Fact]
    public async Task DisposingStopsFollowingRenames()
    {
        var (session, cls, graph) = await OpenAsync();
        BreadcrumbsViewModel breadcrumbs = Create(session, graph);
        string before = breadcrumbs.Segments[1].Name;
        breadcrumbs.Dispose();

        cls.Name = "Renamed";
        rig.Shell.NotifyModelRenamed();

        Assert.Equal(before, breadcrumbs.Segments[1].Name);
    }
}
