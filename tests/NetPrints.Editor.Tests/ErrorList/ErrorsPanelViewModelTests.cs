using NetPrints.Compilation;
using NetPrints.Core;
using NetPrints.Editor.ErrorList;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Tests.ProjectTree;
using NetPrints.Editor.Tests.Shell;
using DocumentId = NetPrints.Editor.Shell.DocumentId;

namespace NetPrints.Editor.Tests.ErrorList;

public sealed class ErrorsPanelViewModelTests : IAsyncDisposable
{
    private readonly ShellPanelRig rig = new();

    public ValueTask DisposeAsync() => rig.DisposeAsync();

    private static DocumentId ClassDocument(ProjectSessionViewModel session, ClassGraph cls) =>
        DocumentId.Graph(session.ClassPathOf(cls), DocumentId.ClassGraphKey);

    private static void Activate(ShellViewModel shell, DocumentId id) =>
        shell.ActiveDocument = shell.AddDocument(new TestDocumentViewModel(id, "doc"));

    private static CodeDiagnostic Diagnostic(ClassGraph cls, string id, string? graphKey = null, string? nodeId = null) =>
        new(CodeDiagnosticSeverity.Error, id, "boom", cls.FullName, graphKey, nodeId, null, null);

    private static void SetBuildDiagnostics(ProjectSessionViewModel session, params CodeDiagnostic[] diagnostics) =>
        session.Project.LastDiagnostics = new ObservableRangeCollection<CodeDiagnostic>(diagnostics);

    [Fact]
    public async Task ThePanelListsTheWholeProjectsDiagnosticsLabelledByClassWhateverTheActiveDocument()
    {
        Assert.Null(rig.Errors.Current);
        ProjectSessionViewModel session = await rig.OpenSessionAsync();
        ClassGraph first = session.Project.Classes[0];
        var second = new ClassGraph { Name = "Other", Namespace = first.Namespace, Project = session.Project };
        session.Project.Classes.Add(second);
        SetBuildDiagnostics(session, Diagnostic(first, "CS0001"), Diagnostic(second, "CS0002"), Diagnostic(second, "CS0003"));

        Assert.Equal(["CS0001", "CS0002", "CS0003"], rig.Errors.Current?.Rows.Select(row => row.Id));
        Assert.Equal([first.FullName, second.FullName, second.FullName], rig.Errors.Current?.Rows.Select(row => row.ClassFullName));

        Activate(rig.Shell, ClassDocument(session, first));
        Assert.Equal(3, rig.Errors.Current?.Rows.Count);

        rig.Shell.Session = null;
        Assert.Null(rig.Errors.Current);
    }

    [Fact]
    public async Task AnErrorInAClassWithNoOpenTabIsListedAndActivatingItOpensThatClassesTab()
    {
        ProjectSessionViewModel session = await rig.OpenSessionAsync();
        ClassGraph first = session.Project.Classes[0];
        var second = new ClassGraph { Name = "Other", Namespace = first.Namespace, Project = session.Project };
        session.Project.Classes.Add(second);
        Activate(rig.Shell, ClassDocument(session, first));
        SetBuildDiagnostics(session, Diagnostic(second, "CS0002", GraphKeys.For(second)));
        ErrorListViewModel list = rig.Errors.Current ?? throw new InvalidOperationException("No error list.");

        list.NavigateCommand.Execute(list.Rows.Single());

        Assert.Contains($"OpenDocument:{ClassDocument(session, second)}", rig.Api.Calls);
    }

    [Fact]
    public async Task ActivatingARowOpensItsGraphsTabAndSelectsTheNode()
    {
        ProjectSessionViewModel session = await rig.OpenSessionAsync();
        ClassGraph cls = session.Project.Classes[0];
        MethodGraph method = cls.Methods.First();
        string nodeId = method.Nodes.First().Id;
        using var graph = new NodeGraphViewModel(method, session.ContextFor(cls).Services);
        var document = new GraphDocumentViewModel(
            DocumentId.Graph(session.ClassPathOf(cls), DocumentId.MethodKeyPrefix + method.Id), graph, cls, session);
        rig.Api.Opened = _ => rig.Shell.ActiveDocument = rig.Shell.AddDocument(document);
        Activate(rig.Shell, ClassDocument(session, cls));
        SetBuildDiagnostics(session, Diagnostic(cls, "CS1503", GraphKeys.For(method), nodeId));
        ErrorListViewModel list = rig.Errors.Current ?? throw new InvalidOperationException("No error list.");

        list.NavigateCommand.Execute(list.Rows.Single());

        Assert.Contains($"OpenDocument:{document.Id}", rig.Api.Calls);
        Assert.Equal(nodeId, document.Graph.SelectedNodes.Single().Node.Id);
    }

    [Fact]
    public async Task ActivatingARowWhoseGraphIsOpenActivatesItAndOpensNoSecondTab()
    {
        ProjectSessionViewModel session = await rig.OpenSessionAsync();
        ClassGraph cls = session.Project.Classes[0];
        MethodGraph method = cls.Methods.First();
        DocumentId id = DocumentId.Graph(session.ClassPathOf(cls), DocumentId.MethodKeyPrefix + method.Id);
        rig.Api.OpenDocument(id);
        Activate(rig.Shell, ClassDocument(session, cls));
        SetBuildDiagnostics(session, Diagnostic(cls, "CS0161", GraphKeys.For(method)));
        ErrorListViewModel list = rig.Errors.Current ?? throw new InvalidOperationException("No error list.");

        rig.Api.OpenDocument(ClassDocument(session, cls));
        list.NavigateCommand.Execute(list.Rows.Single());

        Assert.Equal(id, rig.Api.ActiveDocument);
        Assert.Equal(2, rig.Api.OpenDocuments.Count);
    }

    [Fact]
    public async Task ARowWhoseGraphNoLongerExistsOpensNothing()
    {
        ProjectSessionViewModel session = await rig.OpenSessionAsync();
        ClassGraph cls = session.Project.Classes[0];
        Activate(rig.Shell, ClassDocument(session, cls));
        SetBuildDiagnostics(session, Diagnostic(cls, "CS0161", "method-that-is-gone"));
        ErrorListViewModel list = rig.Errors.Current ?? throw new InvalidOperationException("No error list.");

        list.NavigateCommand.Execute(list.Rows.Single());

        Assert.DoesNotContain(rig.Api.Calls, call => call.StartsWith("OpenDocument", StringComparison.Ordinal));
    }
}
