using NetPrints.Core;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Navigation;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Tests.ProjectTree;
using NetPrints.Editor.Tests.Shell;

namespace NetPrints.Editor.Tests.Navigation;

public sealed class NavigationServiceTests : IAsyncDisposable
{
    private readonly ShellPanelRig rig = new();
    private readonly List<GraphDocumentViewModel> documents = [];
    private readonly List<NodeGraphViewModel> graphs = [];

    public ValueTask DisposeAsync()
    {
        graphs.ForEach(graph => graph.Dispose());
        return rig.DisposeAsync();
    }

    private async Task<(ProjectSessionViewModel Session, GraphDocumentViewModel Class, GraphDocumentViewModel Method, NavigationService Service)> SetUpAsync()
    {
        ProjectSessionViewModel session = await rig.OpenSessionAsync();
        ClassGraph cls = session.Project.Classes[0];
        MethodGraph method = cls.Methods.First();
        GraphDocumentViewModel classDocument = Document(session, cls, cls, DocumentId.ClassGraphKey);
        GraphDocumentViewModel methodDocument = Document(session, cls, method, DocumentId.MethodKeyPrefix + method.Id);
        rig.Api.Opened = id =>
        {
            if (documents.Find(document => document.Id == id) is { } document)
            {
                rig.Shell.ActiveDocument = rig.Shell.AddDocument(document);
            }
        };
        var service = new NavigationService(rig.Shell, rig.Api);
        rig.Api.OpenDocument(classDocument.Id);
        return (session, classDocument, methodDocument, service);
    }

    private GraphDocumentViewModel Document(ProjectSessionViewModel session, ClassGraph cls, NodeGraph model, string key)
    {
        var graph = new NodeGraphViewModel(model, session.ContextFor(cls).Services);
        graphs.Add(graph);
        var document = new GraphDocumentViewModel(DocumentId.Graph(session.ClassPathOf(cls), key), graph, cls, session);
        documents.Add(document);
        return document;
    }

    [Fact]
    public async Task NavigatingRecordsTheViewAndBackRestoresGraphViewportAndSelection()
    {
        var (_, classDocument, methodDocument, service) = await SetUpAsync();
        classDocument.ViewportLocation = new GraphPoint(120, 80);
        classDocument.ViewportZoom = 1.5;
        var selected = classDocument.Graph.Nodes.First();
        classDocument.Graph.SelectNodes([selected], deselectPrevious: true);
        string targetNode = methodDocument.Graph.Nodes.First().Node.Id;

        Assert.True(service.NavigateTo(new NavigationTarget(methodDocument.Id, targetNode)));

        Assert.Equal(methodDocument.Id, rig.Shell.ActiveDocument?.Id);
        Assert.Equal(targetNode, methodDocument.Graph.SelectedNodes.Single().Node.Id);
        Assert.True(service.CanGoBack);
        classDocument.Graph.DeselectNodes();
        classDocument.ViewportLocation = new GraphPoint(0, 0);
        classDocument.ViewportZoom = 1;

        service.GoBack();

        Assert.Equal(classDocument.Id, rig.Shell.ActiveDocument?.Id);
        Assert.Equal(new GraphPoint(120, 80), classDocument.ViewportLocation);
        Assert.Equal(1.5, classDocument.ViewportZoom);
        Assert.Equal(selected.Node.Id, classDocument.Graph.SelectedNodes.Single().Node.Id);
        Assert.True(service.CanGoForward);

        service.GoForward();

        Assert.Equal(methodDocument.Id, rig.Shell.ActiveDocument?.Id);
        Assert.False(service.CanGoForward);
    }

    [Fact]
    public async Task ASwitchBetweenTabsIsRecordedSoBackReturnsToTheTabLeft()
    {
        var (_, classDocument, methodDocument, service) = await SetUpAsync();
        rig.Api.OpenDocument(methodDocument.Id);
        Assert.True(service.CanGoBack);

        service.GoBack();

        Assert.Equal(classDocument.Id, rig.Shell.ActiveDocument?.Id);
        Assert.True(service.CanGoForward);
    }

    [Fact]
    public async Task AGraphThatNoLongerExistsIsSkippedAndNavigatingToItDoesNothing()
    {
        var (session, classDocument, methodDocument, service) = await SetUpAsync();
        service.NavigateTo(new NavigationTarget(methodDocument.Id));
        var gone = DocumentId.Graph(session.ClassPathOf(session.Project.Classes[0]), DocumentId.MethodKeyPrefix + "gone");

        Assert.False(service.NavigateTo(new NavigationTarget(gone)));

        service.GoBack();
        Assert.Equal(classDocument.Id, rig.Shell.ActiveDocument?.Id);
        Assert.False(service.CanGoBack);
    }
}
