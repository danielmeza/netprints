using System.Collections.ObjectModel;
using System.Text.Json;
using NetPrints.Core;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Tests.Shell;
using NetPrints.Extensibility.Hosting;
using NetPrints.Projects;
using NetPrints.Reflection;

namespace NetPrints.Editor.Tests.Hosting;

/// <summary>
/// EX-T08: the host channel drives the editor. A types-changed message reloads the reflection provider once, a
/// focus-document message opens the class, anything else is ignored.
/// </summary>
public sealed class HostChannelBridgeTests : IDisposable
{
    private static readonly JsonElement NoPayload = JsonDocument.Parse("{}").RootElement.Clone();

    private readonly InMemoryHostChannel host;
    private readonly SpyReflectionHost reflection;
    private readonly TestEditor editor;
    private readonly Dictionary<ClassGraph, ClassEditorViewModel> editors = [];
    private readonly ProjectRig rig;
    private readonly FakeShell shell = new();
    private readonly string csproj = TestPaths.CopyHelloWorldSample();

    public HostChannelBridgeTests(IReflectionHost sharedReflection)
    {
        (InMemoryHostChannel editorEnd, host) = InMemoryHostChannel.CreatePair("test");
        reflection = new SpyReflectionHost(sharedReflection);
        editor = TestEditor.Create(_ => reflection, hostChannel: editorEnd);
        rig = new ProjectRig(editor.Context, EditorOf);
        rig.Actions.Api = shell;
        shell.Opened = OpenGraphDocument;
    }

    public void Dispose()
    {
        rig.Dispose();
        TestPaths.TryDelete(csproj);
    }

    private ClassEditorViewModel EditorOf(ClassGraph cls)
    {
        if (!editors.TryGetValue(cls, out ClassEditorViewModel? classEditor))
        {
            classEditor = new ClassEditorViewModel(cls, editor.Context);
            editors[cls] = classEditor;
        }

        return classEditor;
    }

    // What the shell host's document factory does for a graph document.
    private void OpenGraphDocument(DocumentId id)
    {
        ProjectSessionViewModel session = rig.Session ?? throw new InvalidOperationException("No session.");
        NodeGraph graph = CommandTargets.GraphOf(session, id) ?? throw new InvalidOperationException("No graph.");
        ClassGraph cls = graph as ClassGraph ?? graph.Class ?? throw new InvalidOperationException("No class.");
        rig.Shell.AddDocument(new GraphDocumentViewModel(id, new NodeGraphViewModel(graph, EditorOf(cls).Services), cls, session));
    }

    private static HostMessage FocusMessage(string path) =>
        new(HostMessageTypes.FocusDocument, JsonSerializer.SerializeToElement(new { path }));

    [Fact]
    public async Task TypesChangedReloadsTheReflectionProviderOnce()
    {
        await rig.LoadProjectAsync(csproj);
        int before = reflection.Reloads;

        await host.SendAsync(new HostMessage(HostMessageTypes.TypesChanged, NoPayload), TestContext.Current.CancellationToken);

        Assert.Equal(before + 1, reflection.Reloads);
    }

    [Fact]
    public async Task AnUnknownMessageTypeIsIgnored()
    {
        await rig.LoadProjectAsync(csproj);
        int before = reflection.Reloads;

        await host.SendAsync(new HostMessage("acme.something-else", NoPayload), TestContext.Current.CancellationToken);

        Assert.Equal(before, reflection.Reloads);
        Assert.Empty(shell.Calls);
        Assert.Empty(editor.Dialogs.Errors);
    }

    [Fact]
    public async Task TypesChangedWithNoProjectOpenDoesNothing()
    {
        await host.SendAsync(new HostMessage(HostMessageTypes.TypesChanged, NoPayload), TestContext.Current.CancellationToken);

        Assert.Equal(0, reflection.Reloads);
    }

    [Fact]
    public async Task FocusDocumentOpensTheClassGraphAndOpensItAgainTheSecondTime()
    {
        await rig.LoadProjectAsync(csproj);
        ClassGraph cls = Assert.Single(rig.Project?.Classes ?? []);
        string graphPath = Path.Combine(Path.GetDirectoryName(csproj) ?? "", "HelloWorld.Program.netpc.json");
        DocumentId expected = CommandTargets.GraphDocumentOf(rig.Session ?? throw new InvalidOperationException("No session."), cls)
            ?? throw new InvalidOperationException("No document id.");

        await host.SendAsync(FocusMessage("HelloWorld.Program.netpc.json"), TestContext.Current.CancellationToken);
        Assert.Equal([$"OpenDocument:{expected}"], shell.Calls);

        await host.SendAsync(FocusMessage(graphPath), TestContext.Current.CancellationToken);
        Assert.Equal([$"OpenDocument:{expected}", $"OpenDocument:{expected}"], shell.Calls);
        Assert.Single(rig.Shell.Documents);
    }

    [Fact]
    public async Task FocusDocumentWithANodeIdRevealsTheNode()
    {
        // R2-21: HelloWorld.Program's return node is "n000000000vny2" (see ExtensionPersistenceTests).
        await rig.LoadProjectAsync(csproj);

        await host.SendAsync(new HostMessage(HostMessageTypes.FocusDocument,
            JsonSerializer.SerializeToElement(new { path = "HelloWorld.Program.netpc.json", nodeId = "n000000000vny2" })),
            TestContext.Current.CancellationToken);

        var document = Assert.IsType<GraphDocumentViewModel>(Assert.Single(rig.Shell.Documents));
        var revealed = Assert.Single(document.Graph.SelectedNodes);
        Assert.Equal("n000000000vny2", revealed.Node.Id);
    }

    [Fact]
    public async Task FocusDocumentForAnUnknownPathOrWithoutOneIsIgnored()
    {
        await rig.LoadProjectAsync(csproj);

        await host.SendAsync(FocusMessage("Nowhere.netpc.json"), TestContext.Current.CancellationToken);
        await host.SendAsync(new HostMessage(HostMessageTypes.FocusDocument, NoPayload), TestContext.Current.CancellationToken);

        Assert.Empty(shell.Calls);
        Assert.Empty(editor.Dialogs.Errors);
    }

    [Fact]
    public async Task DisposingTheActionsStopsListening()
    {
        await rig.LoadProjectAsync(csproj);
        rig.Dispose();
        int before = reflection.Reloads;

        await host.SendAsync(new HostMessage(HostMessageTypes.TypesChanged, NoPayload), TestContext.Current.CancellationToken);

        Assert.Equal(before, reflection.Reloads);
    }

    [Fact]
    public async Task SendingAfterDisposeThrows()
    {
        await host.DisposeAsync();

        Assert.Throws<InvalidOperationException>(() => host.SendAsync(new HostMessage(HostMessageTypes.TypesChanged, NoPayload), TestContext.Current.CancellationToken));
    }

    /// <summary>Counts reloads without doing them; every other member is the loaded shared host's.</summary>
    private sealed class SpyReflectionHost(IReflectionHost inner) : IReflectionHost
    {
        public int Reloads { get; private set; }

        public bool IsLoaded => inner.IsLoaded;

        public Task Loaded => inner.Loaded;

        public IReflectionProvider Provider => inner.Provider;

        public ProjectSnapshot? Snapshot => inner.Snapshot;

        public ReadOnlyObservableCollection<TypeSpecifier> NonStaticTypes => inner.NonStaticTypes;

        public IReadOnlyList<string> LastWarnings => inner.LastWarnings;

        public event EventHandler? Reloaded
        {
            add => inner.Reloaded += value;
            remove => inner.Reloaded -= value;
        }

        public Task ReloadAsync(Project project, CancellationToken cancellationToken = default)
        {
            Reloads++;
            return Task.CompletedTask;
        }
    }
}
