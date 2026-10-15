using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Core;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Editor.UndoRedo;
using NetPrints.Projects;
using NetPrints.Reflection;

namespace NetPrints.Editor.Tests.Shell;

/// <summary>A closed graph document lets go of its graph, which would otherwise stay subscribed to the app-lifetime reflection host (Review C R2).</summary>
public sealed class GraphDocumentDisposalTests(IReflectionHost shared)
{
    private sealed class CountingReflectionHost(IReflectionHost inner) : IReflectionHost
    {
        public int Subscribers { get; private set; }

        public bool IsLoaded => inner.IsLoaded;

        public Task Loaded => inner.Loaded;

        public IReflectionProvider Provider => inner.Provider;

        public ProjectSnapshot? Snapshot => inner.Snapshot;

        public ReadOnlyObservableCollection<TypeSpecifier> NonStaticTypes => inner.NonStaticTypes;

        public IReadOnlyList<string> LastWarnings => inner.LastWarnings;

        public Task ReloadAsync(Project project, CancellationToken cancellationToken = default) => inner.ReloadAsync(project, cancellationToken);

        public event EventHandler? Reloaded
        {
            add
            {
                Subscribers++;
                inner.Reloaded += value;
            }

            remove
            {
                Subscribers--;
                inner.Reloaded -= value;
            }
        }
    }

    private sealed class NoServices : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private sealed class Rig : IAsyncDisposable
    {
        private readonly TestEditor editor;
        private readonly ClassContext classContext;

        public Rig(IReflectionHost shared)
        {
            Reflection = new CountingReflectionHost(shared);
            editor = new TestEditor(Reflection);
            Class = new ClassGraph { Name = "C", Namespace = "N" };
            classContext = new ClassContext(Class, editor.Context, new UndoRedoStack());
            Method = classContext.CreateMethod();
            var registry = new ContributionRegistry(NullLogger<ContributionRegistry>.Instance);
            BuiltInContributions.Register(registry);
            registry.Freeze();
            Shell = new ShellViewModel(registry, new NoServices(), TimeProvider.System, new InlineDispatcher());
        }

        public CountingReflectionHost Reflection { get; }

        public ClassGraph Class { get; }

        public MethodGraph Method { get; }

        public ShellViewModel Shell { get; }

        public GraphDocumentViewModel NewDocument(string key) =>
            GraphDocumentFactory.Open(DocumentId.Graph("C.cs", key), Method, classContext.Services, Class, session: null, invoker: null);

        public async ValueTask DisposeAsync()
        {
            Shell.Dispose();
            classContext.Dispose();
            await editor.DisposeAsync();
        }
    }

    [Fact]
    public async Task RemovingAGraphDocumentReleasesItsGraphsReflectionSubscription()
    {
        await using var rig = new Rig(shared);
        int before = rig.Reflection.Subscribers;
        GraphDocumentViewModel document = Assert.IsType<GraphDocumentViewModel>(rig.Shell.AddDocument(rig.NewDocument("method:1")));
        Assert.True(rig.Reflection.Subscribers > before, "an open graph listens to reflection reloads");

        Assert.True(rig.Shell.RemoveDocument(document.Id));

        Assert.Equal(before, rig.Reflection.Subscribers);
    }

    [Fact]
    public async Task DisposingTheShellReleasesTheSubscriptionsOfTheGraphsStillOpen()
    {
        await using var rig = new Rig(shared);
        int before = rig.Reflection.Subscribers;
        rig.Shell.AddDocument(rig.NewDocument("method:1"));
        rig.Shell.AddDocument(rig.NewDocument("method:2"));

        rig.Shell.Dispose();

        Assert.Equal(before, rig.Reflection.Subscribers);
    }
}
