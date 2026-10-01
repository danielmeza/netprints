using NetPrints.Core;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Tests.Graph;
using NetPrints.Editor.Tests.Hosting;

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
