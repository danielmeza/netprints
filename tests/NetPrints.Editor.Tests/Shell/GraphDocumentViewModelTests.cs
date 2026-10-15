using NetPrints.Core;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Tests.Graph;
using NetPrints.Editor.Tests.Hosting;
using ProjectLoadResult = NetPrints.Serialization.ProjectLoadResult;

namespace NetPrints.Editor.Tests.Shell;

public sealed class GraphDocumentViewModelTests(TestEditor editor) : GraphTestBase(editor)
{
    private static readonly DocumentId Id = DocumentId.Graph("C.cs", "method:1");

    [Fact]
    public void ItWrapsTheGraphAndTitlesItWithItsName()
    {
        var document = new GraphDocumentViewModel(Id, Graph, Class, session: null);

        Assert.Same(Graph, document.Graph);
        Assert.Equal(Id, document.Id);
        Assert.Equal(Graph.Name, document.Title);
        Assert.False(document.IsUnsaved);
    }

    [Fact]
    public void AnUnsavedClassFileMarksTheTitleUntilItIsSaved()
    {
        var document = new GraphDocumentViewModel(Id, Graph, Class, session: null);

        Class.MarkDirty();
        document.Refresh();
        Assert.True(document.IsUnsaved);
        Assert.Equal(Graph.Name + "*", document.Title);

        Class.MarkClean();
        document.Refresh();
        Assert.False(document.IsUnsaved);
        Assert.Equal(Graph.Name, document.Title);
    }

    [Fact]
    public async Task ATabFollowsTheSessionsUnsavedStateOfItsClassFile()
    {
        string path = TestPaths.CopyHelloWorldSample();
        try
        {
            ProjectLoadResult loaded = await Editor.Persistence.LoadAsync(path, TestContext.Current.CancellationToken);
            using var session = new ProjectSessionViewModel(loaded.Project, Editor.Context);
            ClassGraph cls = loaded.Project.Classes.Single();
            ClassContext context = session.ContextFor(cls);
            ExecutionGraph method = context.Methods.First().Graph;
            using var graph = new NodeGraphViewModel(method, context.Services);
            using var document = new GraphDocumentViewModel(Id, graph, cls, session);
            Assert.Equal(graph.Name, document.Title);

            method.Nodes.First().PositionX += 10;
            Assert.True(document.IsUnsaved);
            Assert.Equal(graph.Name + "*", document.Title);

            Assert.True(await session.SaveAllAsync());
            Assert.False(document.IsUnsaved);
            Assert.Equal(graph.Name, document.Title);
        }
        finally
        {
            TestPaths.TryDelete(path);
        }
    }

    [Fact]
    public void TheViewportStartsAtTheOriginAtFullZoomAndKeepsWhatTheCanvasReports()
    {
        var document = new GraphDocumentViewModel(Id, Graph, Class, session: null);
        Assert.Equal(new GraphPoint(0, 0), document.ViewportLocation);
        Assert.Equal(1d, document.ViewportZoom);

        document.ViewportLocation = new GraphPoint(120, -40);
        document.ViewportZoom = 0.5;

        Assert.Equal(new GraphPoint(120, -40), document.ViewportLocation);
        Assert.Equal(0.5, document.ViewportZoom);
    }
}
