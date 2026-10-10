using System.Collections.ObjectModel;
using NetPrints.Core;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Tests.Graph;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Graph;
using NetPrints.Projects;
using NetPrints.Reflection;
using NetPrints.Testing;

namespace NetPrints.Editor.Tests.Search;

/// <summary>FR-091: the search opened from a pin whose type a covering catalog does not list says which catalog hides it.</summary>
public sealed class HiddenTypeSearchTests : GraphTestBase, IAsyncDisposable
{
    private const string CatalogId = "core";
    private const string DefaultMessage = "No node matches your search.";

    private readonly TestEditor owned;

    public HiddenTypeSearchTests()
        : this(new TestEditor(new FixedReflectionHost(CreateProvider())))
    {
    }

    private HiddenTypeSearchTests(TestEditor owned)
        : base(owned)
    {
        this.owned = owned;
    }

    public async ValueTask DisposeAsync()
    {
        Dispose();
        await owned.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    private static MemoizedReflectionProvider CreateProvider()
    {
        string coreLib = typeof(object).Assembly.GetName().Name ?? string.Empty;
        var catalog = new InMemoryTypeCatalog(
            new CatalogInfo(CatalogId, "1.0", [coreLib]),
            [], [], [], [],
            new Dictionary<TypeSpecifier, IReadOnlyList<string>>(),
            new Dictionary<MethodSpecifier, string>());
        var live = new ReflectionProvider([.. TestSnapshots.RuntimeAssemblyPaths().Select(path => new ResolvedAssembly(path, null))], [], new HashSet<string>([coreLib]));
        return new MemoizedReflectionProvider(new CompositeReflectionProvider([catalog, live]));
    }

    [Fact(Timeout = 120000)]
    public async Task ThePinOfAHiddenTypeNamesTheCatalogInTheEmptyMessage()
    {
        var upper = new CallMethodNode(Method, new MethodSpecifier("ToUpperInvariant", [], [StringType], MethodModifiers.None, MemberVisibility.Public, StringType, []));
        var output = upper.OutputDataPins.First(pin => pin.Name != "Exception");

        await Graph.OpenSearchAsync(new GraphPoint(10, 10), output, TestContext.Current.CancellationToken);

        Assert.Equal(CatalogId, Graph.Search.HidingCatalogId);
        Assert.Contains(CatalogId, Graph.Search.EmptyMessage, StringComparison.Ordinal);
        Assert.Contains("hidden", Graph.Search.EmptyMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(Timeout = 120000)]
    public async Task ASearchWithNoPinKeepsTheDefaultEmptyMessage()
    {
        await Graph.OpenSearchAsync(new GraphPoint(10, 10), null, TestContext.Current.CancellationToken);

        Assert.Null(Graph.Search.HidingCatalogId);
        Assert.Equal(DefaultMessage, Graph.Search.EmptyMessage);
    }

    [Fact(Timeout = 120000)]
    public async Task ReopeningWithoutAHiddenPinClearsTheCatalog()
    {
        var upper = new CallMethodNode(Method, new MethodSpecifier("ToUpperInvariant", [], [StringType], MethodModifiers.None, MemberVisibility.Public, StringType, []));
        var output = upper.OutputDataPins.First(pin => pin.Name != "Exception");
        await Graph.OpenSearchAsync(new GraphPoint(10, 10), output, TestContext.Current.CancellationToken);

        await Graph.OpenSearchAsync(new GraphPoint(10, 10), null, TestContext.Current.CancellationToken);

        Assert.Null(Graph.Search.HidingCatalogId);
        Assert.Equal(DefaultMessage, Graph.Search.EmptyMessage);
    }

    private sealed class FixedReflectionHost(IReflectionProvider provider) : IReflectionHost
    {
        public bool IsLoaded => true;

        public Task Loaded => Task.CompletedTask;

        public IReflectionProvider Provider { get; } = provider;

        public ProjectSnapshot? Snapshot => null;

        public ReadOnlyObservableCollection<TypeSpecifier> NonStaticTypes { get; } = new([]);

        public IReadOnlyList<string> LastWarnings => [];

        public event EventHandler? Reloaded
        {
            add { }
            remove { }
        }

        public Task ReloadAsync(Project project, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
