using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Core;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Graph.Nodes;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Tests.Graph;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Editor.UndoRedo;
using NetPrints.Graph;
using NetPrints.Serialization;
using DocumentId = NetPrints.Editor.Shell.DocumentId;

namespace NetPrints.Editor.Tests.Shell;

/// <summary>The shell's context provider reads the active graph document and pulses for the same triggers as the class editor's provider.</summary>
public sealed class ShellCommandContextProviderTests(TestEditor testEditor) : GraphTestBase(testEditor)
{
    private sealed class NoServices : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private static readonly DocumentId MainId = DocumentId.Graph("C.cs", "method:1");

    private (ShellViewModel Shell, ShellCommandContextProvider Provider, FakeShell Services, GraphDocumentViewModel Document) Create()
    {
        var registry = new ContributionRegistry(NullLogger<ContributionRegistry>.Instance);
        BuiltInContributions.Register(registry);
        registry.Freeze();
        var shell = new ShellViewModel(registry, new NoServices(), TimeProvider.System, new InlineDispatcher());
        var services = new FakeShell();
        var document = new GraphDocumentViewModel(MainId, Graph, Class, session: null);
        shell.AddDocument(document);
        return (shell, new ShellCommandContextProvider(shell, services), services, document);
    }

    [Fact]
    public void TheContextHoldsTheShellTheSessionTheActiveDocumentAndItsGraphAndSelection()
    {
        (ShellViewModel shell, ShellCommandContextProvider provider, FakeShell services, GraphDocumentViewModel document) = Create();
        using (shell)
        {
            CommandContext none = provider.Create("parameter");
            Assert.Same(services, none.Shell);
            Assert.Null(none.Session);
            Assert.Null(none.ActiveDocument);
            Assert.Null(none.ActiveGraph);
            Assert.Empty(none.Selection.Nodes);
            Assert.Equal("parameter", none.Parameter);

            shell.ActiveDocument = document;
            Node added = Graph.AddNode<IfElseNode>(new GraphPoint(200, 100));
            NodeViewModel node = Graph.Nodes.Single(vm => vm.Node == added);
            Graph.SelectNodes([node], deselectPrevious: true);
            CommandContext active = provider.Create();

            Assert.Equal(MainId, active.ActiveDocument);
            Assert.Same(Graph, active.ActiveGraph);
            Assert.Same(node, Assert.Single(active.Selection.Nodes));
        }
    }

    [Fact]
    public void ChangingTheActiveDocumentOrTheActiveGraphsSelectionPulses()
    {
        (ShellViewModel shell, ShellCommandContextProvider provider, _, GraphDocumentViewModel document) = Create();
        using (shell)
        {
            int pulses = 0;
            provider.CommandStatesChanged += (_, _) => pulses++;

            shell.ActiveDocument = document;
            Assert.Equal(1, pulses);

            Node added = Graph.AddNode<IfElseNode>(new GraphPoint(200, 100));
            int before = pulses;
            Graph.SelectNodes([Graph.Nodes.Single(vm => vm.Node == added)], deselectPrevious: true);
            Assert.True(pulses > before, "selecting a node pulses");

            shell.ActiveDocument = null;
            before = pulses;
            Graph.SelectNodes([], deselectPrevious: true);
            Assert.Equal(before, pulses);
        }
    }

    [Fact]
    public async Task ReplacingTheSessionAndAnEditToItsUndoHistoryPulse()
    {
        string path = TestPaths.CopyHelloWorldSample();
        try
        {
            ProjectLoadResult loaded = await Editor.Persistence.LoadAsync(path, TestContext.Current.CancellationToken);
            using var session = new ProjectSessionViewModel(loaded.Project, Editor.Context);
            (ShellViewModel shell, ShellCommandContextProvider provider, _, _) = Create();
            using (shell)
            {
                int pulses = 0;
                provider.CommandStatesChanged += (_, _) => pulses++;

                shell.Session = session;
                Assert.Equal(1, pulses);
                Assert.Same(session, provider.Create().Session);

                session.UndoStackFor(loaded.Project.Classes.Single()).Do(new DelegateUndoableCommand("edit", () => { }, () => { }));
                Assert.Equal(2, pulses);
            }
        }
        finally
        {
            TestPaths.TryDelete(path);
        }
    }

    [Fact]
    public void AnUnsubscribedProviderStopsWatchingTheShellAndTheGraph()
    {
        (ShellViewModel shell, ShellCommandContextProvider provider, _, GraphDocumentViewModel document) = Create();
        using (shell)
        {
            int pulses = 0;
            EventHandler handler = (_, _) => pulses++;
            provider.CommandStatesChanged += handler;
            shell.ActiveDocument = document;
            provider.CommandStatesChanged -= handler;
            int atUnsubscribe = pulses;

            shell.ActiveDocument = null;
            shell.ActiveDocument = document;
            Node added = Graph.AddNode<IfElseNode>(new GraphPoint(200, 100));
            Graph.SelectNodes([Graph.Nodes.Single(vm => vm.Node == added)], deselectPrevious: true);

            Assert.Equal(atUnsubscribe, pulses);
        }
    }
}
