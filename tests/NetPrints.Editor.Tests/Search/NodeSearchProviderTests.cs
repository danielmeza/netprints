using NetPrints.Core;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.NodeSearch;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Serialization.Documents;

namespace NetPrints.Editor.Tests.Search;

/// <summary>Providers of the node search (FR-016, research R10 SUG1): ownership, order and the build-once rule.</summary>
public sealed class NodeSearchProviderTests : FixtureGraphTestBase
{
    private readonly IReflectionHost reflection;

    public NodeSearchProviderTests(IReflectionHost sharedReflection)
        : base(sharedReflection)
    {
        reflection = sharedReflection;
    }

    private static readonly string[] ExpectedOrder =
    [
        "makeDelegate", "nodeKinds", "pinVariables", "pinMethods", "thisVariables", "thisMethods", "staticMethods",
        "staticVariables", "localVariables", "types", "typePin", "eventEntries", "extensionNodes",
    ];

    private sealed class CountingProvider : INodeSearchProvider
    {
        public CountingProvider(Action onBuilt)
        {
            onBuilt();
        }

        public string Id => "counting";

        public string OwnerKind => BuiltInNodeKinds.Type;

        public IEnumerable<NodeSearchEntry> Suggest(NodeSearchContext context) =>
            [new TypeEntry("Counting", TypeSpecifier.FromType<int>())];
    }

    [Fact]
    public void TheDefaultProvidersAreInTheFixedOrder()
    {
        Assert.Equal(ExpectedOrder, NodeSearchProviders.CreateDefault().Select(provider => provider.Id).ToArray());
    }

    [Fact]
    public void EveryProviderIsOwnedByANodeKind()
    {
        string[] known = [NodeSearchProviders.AnyKind, BuiltInNodeKinds.CallMethod, BuiltInNodeKinds.VariableGetter, BuiltInNodeKinds.Type, BuiltInNodeKinds.MakeDelegate, BuiltInNodeKinds.EventEntry];

        foreach (INodeSearchProvider provider in NodeSearchProviders.CreateDefault())
        {
            Assert.Contains(provider.OwnerKind, known);
        }

        Assert.Equal(BuiltInNodeKinds.CallMethod, NodeSearchProviders.CreateDefault().Single(p => p.Id == "pinMethods").OwnerKind);
        Assert.Equal(BuiltInNodeKinds.VariableGetter, NodeSearchProviders.CreateDefault().Single(p => p.Id == "localVariables").OwnerKind);
        Assert.Equal(BuiltInNodeKinds.MakeDelegate, NodeSearchProviders.CreateDefault().Single(p => p.Id == "makeDelegate").OwnerKind);
    }

    [Fact]
    public async Task ProvidersAreBuiltOncePerRegistryNeverPerOpen()
    {
        int built = 0;
        var registry = new NodeSearchProviderRegistry(() => [new CountingProvider(() => built++)]);
        using var search = new NodeSearchViewModel(Graph, registry);

        await search.OpenAsync(new GraphPoint(0, 0), null, TestContext.Current.CancellationToken);
        await search.OpenAsync(new GraphPoint(1, 1), null, TestContext.Current.CancellationToken);
        await search.OpenAsync(new GraphPoint(2, 2), null, TestContext.Current.CancellationToken);

        Assert.Equal(1, built);
        Assert.Contains(search.Items, row => row.Entry is TypeEntry { Category: "Counting" });
    }

    [Fact]
    public async Task AnotherExtensionRegistryBuildsTheProvidersAgain()
    {
        int built = 0;
        var registry = new NodeSearchProviderRegistry(() => [new CountingProvider(() => built++)]);
        using var search = new NodeSearchViewModel(Graph, registry);
        await search.OpenAsync(new GraphPoint(0, 0), null, TestContext.Current.CancellationToken);

        await using var otherEditor = new TestEditor(new FixtureReflectionHost(reflection));
        Assert.NotSame(Editor.Context.Extensions.Current, otherEditor.Context.Extensions.Current);
        Assert.Same(registry.For(Editor.Context.Extensions.Current), registry.For(Editor.Context.Extensions.Current));
        Assert.NotSame(registry.For(Editor.Context.Extensions.Current), registry.For(otherEditor.Context.Extensions.Current));
        Assert.Equal(2, built);
    }
}
