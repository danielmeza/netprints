using System.Collections.ObjectModel;
using System.Text.Json;
using NetPrints.Core;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Main;
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
    private readonly MainEditorVM vm;
    private readonly string csproj = TestPaths.CopyHelloWorldSample();

    public HostChannelBridgeTests(IReflectionHost sharedReflection)
    {
        (InMemoryHostChannel editorEnd, host) = InMemoryHostChannel.CreatePair("test");
        reflection = new SpyReflectionHost(sharedReflection);
        editor = TestEditor.Create(_ => reflection, hostChannel: editorEnd);
        vm = new MainEditorVM(editor.Context);
    }

    public void Dispose()
    {
        vm.OnMainWindowClosed();
        TestPaths.TryDelete(csproj);
    }

    private static HostMessage FocusMessage(string path) =>
        new(HostMessageTypes.FocusDocument, JsonSerializer.SerializeToElement(new { path }));

    [Fact]
    public async Task TypesChangedReloadsTheReflectionProviderOnce()
    {
        await vm.LoadProjectAsync(csproj);
        int before = reflection.Reloads;

        await host.SendAsync(new HostMessage(HostMessageTypes.TypesChanged, NoPayload), TestContext.Current.CancellationToken);

        Assert.Equal(before + 1, reflection.Reloads);
    }

    [Fact]
    public async Task AnUnknownMessageTypeIsIgnored()
    {
        await vm.LoadProjectAsync(csproj);
        int before = reflection.Reloads;

        await host.SendAsync(new HostMessage("acme.something-else", NoPayload), TestContext.Current.CancellationToken);

        Assert.Equal(before, reflection.Reloads);
        Assert.Empty(editor.Windows.Open);
        Assert.Empty(editor.Dialogs.Errors);
    }

    [Fact]
    public async Task TypesChangedWithNoProjectOpenDoesNothing()
    {
        await host.SendAsync(new HostMessage(HostMessageTypes.TypesChanged, NoPayload), TestContext.Current.CancellationToken);

        Assert.Equal(0, reflection.Reloads);
    }

    [Fact]
    public async Task FocusDocumentOpensTheClassAndActivatesItTheSecondTime()
    {
        await vm.LoadProjectAsync(csproj);
        string graphPath = Path.Combine(Path.GetDirectoryName(csproj) ?? "", "HelloWorld.Program.netpc.json");

        await host.SendAsync(FocusMessage("HelloWorld.Program.netpc.json"), TestContext.Current.CancellationToken);
        ClassGraph opened = Assert.Single(editor.Windows.Open.Keys);
        Assert.Equal("HelloWorld.Program", opened.FullName);

        await host.SendAsync(FocusMessage(graphPath), TestContext.Current.CancellationToken);
        Assert.Single(editor.Windows.Open);
        Assert.Same(opened, Assert.Single(editor.Windows.Activated));
    }

    [Fact]
    public async Task FocusDocumentWithANodeIdNavigatesToTheNode()
    {
        // R2-21: HelloWorld.Program's return node is "n000000000vny2" (see ExtensionPersistenceTests).
        await vm.LoadProjectAsync(csproj);

        await host.SendAsync(new HostMessage(HostMessageTypes.FocusDocument,
            JsonSerializer.SerializeToElement(new { path = "HelloWorld.Program.netpc.json", nodeId = "n000000000vny2" })),
            TestContext.Current.CancellationToken);

        ClassGraph opened = Assert.Single(editor.Windows.Open.Keys);
        ClassEditorVM classEditor = editor.Windows.Open[opened];
        Assert.NotNull(classEditor.OpenedGraph);
        var revealed = Assert.Single(classEditor.OpenedGraph.SelectedNodes);
        Assert.Equal("n000000000vny2", revealed.Node.Id);
    }

    [Fact]
    public async Task FocusDocumentForAnUnknownPathOrWithoutOneIsIgnored()
    {
        await vm.LoadProjectAsync(csproj);

        await host.SendAsync(FocusMessage("Nowhere.netpc.json"), TestContext.Current.CancellationToken);
        await host.SendAsync(new HostMessage(HostMessageTypes.FocusDocument, NoPayload), TestContext.Current.CancellationToken);

        Assert.Empty(editor.Windows.Open);
        Assert.Empty(editor.Dialogs.Errors);
    }

    [Fact]
    public async Task ClosingTheMainWindowStopsListening()
    {
        await vm.LoadProjectAsync(csproj);
        vm.OnMainWindowClosed();
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
