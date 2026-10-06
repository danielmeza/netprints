using NetPrints.Editor.Graph;
using NetPrints.Editor.Shell;
using NetPrints.Editor.State;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Editor.Tests.Shell;

namespace NetPrints.Editor.Tests.State;

public sealed class SessionStateTests
{
    private const string ProjectPath = "/src/Hello/Hello.csproj";

    private static readonly DocumentId A = DocumentId.Graph("A.cs", "method:1");
    private static readonly DocumentId B = DocumentId.Graph("A.cs", "method:2");
    private static readonly DocumentId C = DocumentId.Graph("A.cs", "method:3");

    private readonly InMemoryEditorFileSystem fs = new();
    private readonly EditorDataPaths paths = new("/state-root");

    private JsonEditorStateStore CreateStore() => new(paths, fs, new CollectingLogger<JsonEditorStateStore>());

    private sealed class ViewportDocument(DocumentId id) : DocumentViewModel(id, id.ToString()), IViewportDocument
    {
        public GraphPoint ViewportLocation { get; set; }

        public double ViewportZoom { get; set; } = 1;
    }

    private sealed class SessionStore : IEditorStateStore
    {
        public SessionState? Session { get; set; }

        public SessionState? LoadSession(string projectPath) => Session;

        public void SaveSession(SessionState state) => Session = state;

        public WindowState? LoadWindow() => throw new NotSupportedException();

        public void SaveWindow(WindowState state) => throw new NotSupportedException();

        public LayoutState? LoadLayout() => throw new NotSupportedException();

        public void SaveLayout(LayoutState state) => throw new NotSupportedException();

        public RecentState LoadRecent() => throw new NotSupportedException();

        public void SaveRecent(RecentState state) => throw new NotSupportedException();
    }

    /// <summary>A shell whose documents are viewport documents, found by id.</summary>
    private sealed class Rig
    {
        public Rig()
        {
            Shell.Opened = id => Documents.TryAdd(id, new ViewportDocument(id));
        }

        public FakeShell Shell { get; } = new();

        public Dictionary<DocumentId, ViewportDocument> Documents { get; } = [];

        public DocumentViewModel? Find(DocumentId id) => Documents.GetValueOrDefault(id);

        public ViewportDocument Open(DocumentId id, double x = 0, double y = 0, double zoom = 1)
        {
            Shell.OpenDocument(id);
            ViewportDocument document = Documents[id];
            document.ViewportLocation = new GraphPoint(x, y);
            document.ViewportZoom = zoom;
            return document;
        }

        public void Save(SessionService service, string projectPath = ProjectPath) => service.Save(projectPath, Shell, Find);

        public void Restore(SessionService service, string projectPath = ProjectPath) => service.Restore(projectPath, Shell, Find);
    }

    private static SessionState Crafted(string[] open, string? active, Dictionary<string, ViewportState>? viewports = null) =>
        new(StateFile.CurrentVersion, ProjectPath, open, active, viewports ?? []);

    [Fact]
    public void TheOpenDocumentsTheActiveOneAndEachViewportRoundTripThroughTheFile()
    {
        var saving = new Rig();
        saving.Open(A, -40, 12.5, 1.25);
        saving.Open(B, 5, 6, 0.5);
        saving.Open(C);
        saving.Shell.ActivateDocument(B);
        saving.Save(new SessionService(CreateStore()));

        var restoring = new Rig();
        restoring.Restore(new SessionService(CreateStore()));

        Assert.Equal([A, B, C], restoring.Shell.OpenDocuments);
        Assert.Equal(B, restoring.Shell.ActiveDocument);
        Assert.Equal(new GraphPoint(-40, 12.5), restoring.Documents[A].ViewportLocation);
        Assert.Equal(1.25, restoring.Documents[A].ViewportZoom);
        Assert.Equal(new GraphPoint(5, 6), restoring.Documents[B].ViewportLocation);
        Assert.Equal(0.5, restoring.Documents[B].ViewportZoom);
        Assert.Equal(GraphPoint.Zero, restoring.Documents[C].ViewportLocation);
    }

    [Fact]
    public void SessionsAreKeptPerProject()
    {
        var first = new Rig();
        first.Open(A);
        first.Save(new SessionService(CreateStore()), "/src/One/One.csproj");
        var second = new Rig();
        second.Open(B);
        second.Open(C);
        second.Save(new SessionService(CreateStore()), "/src/Two/Two.csproj");

        var onOne = new Rig();
        onOne.Restore(new SessionService(CreateStore()), "/src/One/One.csproj");
        var onTwo = new Rig();
        onTwo.Restore(new SessionService(CreateStore()), "/src/Two/Two.csproj");
        var onThree = new Rig();
        onThree.Restore(new SessionService(CreateStore()), "/src/Three/Three.csproj");

        Assert.Equal([A], onOne.Shell.OpenDocuments);
        Assert.Equal([B, C], onTwo.Shell.OpenDocuments);
        Assert.Empty(onThree.Shell.OpenDocuments);
    }

    [Fact]
    public void TheLastInstanceToUnloadWritesTheSession()
    {
        var firstInstance = new Rig();
        firstInstance.Open(A);
        var secondInstance = new Rig();
        secondInstance.Open(B, 9, 9, 0.75);
        secondInstance.Open(C);

        firstInstance.Save(new SessionService(CreateStore()));
        secondInstance.Save(new SessionService(CreateStore()));

        var next = new Rig();
        next.Restore(new SessionService(CreateStore()));
        Assert.Equal([B, C], next.Shell.OpenDocuments);
        Assert.Equal(new GraphPoint(9, 9), next.Documents[B].ViewportLocation);
    }

    [Fact]
    public void UnloadingWithNothingOpenSavesAnEmptySession()
    {
        var before = new Rig();
        before.Open(A);
        before.Save(new SessionService(CreateStore()));
        new Rig().Save(new SessionService(CreateStore()));

        var after = new Rig();
        after.Restore(new SessionService(CreateStore()));

        Assert.Empty(after.Shell.OpenDocuments);
        Assert.Null(after.Shell.ActiveDocument);
    }

    [Fact]
    public void ADocumentThatCannotBeResolvedIsSkippedAndTheRestIsRestoredInOrder()
    {
        var store = new SessionStore { Session = Crafted([A.ToString(), B.ToString(), C.ToString()], A.ToString(), new() { [B.ToString()] = new ViewportState(1, 2, 0.5), [A.ToString()] = new ViewportState(3, 4, 1) }) };
        var rig = new Rig();
        rig.Shell.Unresolvable.Add(A);

        rig.Restore(new SessionService(store));

        Assert.Equal([B, C], rig.Shell.OpenDocuments);
        Assert.Equal(new GraphPoint(1, 2), rig.Documents[B].ViewportLocation);
        Assert.DoesNotContain(A, rig.Documents.Keys);
    }

    [Fact]
    public void TheActiveDocumentFallsBackToTheFirstRestoredOne()
    {
        var store = new SessionStore { Session = Crafted([A.ToString(), B.ToString(), C.ToString()], A.ToString()) };
        var rig = new Rig();
        rig.Shell.Unresolvable.Add(A);

        rig.Restore(new SessionService(store));

        Assert.Equal(B, rig.Shell.ActiveDocument);
    }

    [Fact]
    public void ASavedActiveDocumentThatIsNotInTheListFallsBackToTheFirstRestoredOne()
    {
        var store = new SessionStore { Session = Crafted([A.ToString(), B.ToString()], "graph:Gone.cs#method:9") };
        var rig = new Rig();

        rig.Restore(new SessionService(store));

        Assert.Equal(A, rig.Shell.ActiveDocument);
    }

    [Fact]
    public void ANonFiniteOrNonPositiveViewportIsSkippedAndTheDocumentStillOpens()
    {
        var store = new SessionStore
        {
            Session = Crafted(
                [A.ToString(), B.ToString(), C.ToString()],
                A.ToString(),
                new()
                {
                    [A.ToString()] = new ViewportState(double.NaN, 1, 1),
                    [B.ToString()] = new ViewportState(1, double.PositiveInfinity, 1),
                    [C.ToString()] = new ViewportState(7, 8, 0),
                }),
        };
        var rig = new Rig();

        rig.Restore(new SessionService(store));

        Assert.Equal([A, B, C], rig.Shell.OpenDocuments);
        Assert.All(rig.Documents.Values, document =>
        {
            Assert.Equal(GraphPoint.Zero, document.ViewportLocation);
            Assert.Equal(1, document.ViewportZoom);
        });
    }

    [Fact]
    public void ANonFiniteViewportIsNotSaved()
    {
        var store = new SessionStore();
        var rig = new Rig();
        rig.Open(A, double.NaN, 1, 1);
        rig.Open(B, 2, 3, 0.5);

        rig.Save(new SessionService(store));

        SessionState saved = Assert.IsType<SessionState>(store.Session);
        Assert.Equal([A.ToString(), B.ToString()], saved.OpenDocuments);
        Assert.Equal(ProjectPath, saved.ProjectPath);
        Assert.Equal(B.ToString(), saved.ActiveDocument);
        Assert.Equal([B.ToString()], saved.Viewports.Keys);
        Assert.Equal(new ViewportState(2, 3, 0.5), saved.Viewports[B.ToString()]);
    }

    [Fact]
    public void AnIdThatDoesNotParseIsSkipped()
    {
        var store = new SessionStore { Session = Crafted(["not a document id", DocumentId.ProjectSettings.ToString()], null) };
        var rig = new Rig();

        rig.Restore(new SessionService(store));

        Assert.Equal([DocumentId.ProjectSettings], rig.Shell.OpenDocuments);
        Assert.Equal(DocumentId.ProjectSettings, rig.Shell.ActiveDocument);
    }

    [Fact]
    public void NoSavedSessionOpensNothing()
    {
        var rig = new Rig();

        rig.Restore(new SessionService(new SessionStore()));

        Assert.Empty(rig.Shell.Calls);
    }
}
