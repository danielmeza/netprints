using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Core;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.NodeSearch;
using NetPrints.Editor.Tests.Graph;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Extensibility;
using NetPrints.Extensibility.Loading;
using NetPrints.Extensibility.Nodes;
using NetPrints.Graph;
using NetPrints.Projects;
using NetPrints.Reflection;

namespace NetPrints.Editor.Tests.Search;

/// <summary>FR-021, FR-028, AB-10: a provider an extension adds contributes rows to node search; catalog hints name and key the rows.</summary>
public sealed class ExtensionProviderTests(IReflectionHost sharedReflection)
    : GraphTestBase(TestEditor.Create(_ => sharedReflection, FxHost()))
{
    private const string FxCategory = "Fx Rows";

    private static readonly CSharpFeature FxFeature = new(new CSharpFeatureId("fx.rows"), new CSharpLanguageVersion(12, 0), []);

    private static ExtensionHost FxHost()
    {
        var manifest = new ExtensionManifest("fx", "Fx", "1.0.0", string.Empty, "1.0", []);
        return new ExtensionHost(new ExtensionLoaderOptions([], [], [BuiltInExtension.InProcessEntry, (manifest, new FxExtension())]), NullLoggerFactory.Instance);
    }

    private sealed class FxExtension : INetPrintsExtension
    {
        public void Register(IExtensionBuilder builder) => builder.AddSuggestionProvider(new FxProvider());
    }

    private sealed class FxProvider : INodeSuggestionProvider
    {
        public string Id => "fx.rows";

        public IEnumerable<NodeSearchEntry> Suggest(NodeSearchContext context) => [new FxEntry { Requires = FxFeature }];
    }

    private sealed record FxEntry() : NodeSearchEntry(FxCategory, "Fx row", "Method")
    {
        public override Task<Node?> CreateAsync(INodeCreationContext context, GraphPoint position, NodePin? pin) =>
            Task.FromResult<Node?>(context.Create(position, pin, graph => new TypeNode(graph, TypeSpecifier.FromType<int>())));
    }

    private sealed class FakeHints(NodeHint? hint) : INodeHintSource
    {
        public NodeHint? GetMethodHint(MethodSpecifier method) => hint;

        public NodeHint? GetTypeHint(TypeSpecifier type) => hint;
    }

    [Fact]
    public void TheRowsOfAnExtensionsProviderAreInNodeSearchAfterTheBuiltInOnes()
    {
        List<SuggestionRowViewModel> rows = Graph.Search.BuildItems(null);

        SuggestionRowViewModel fx = Assert.Single(rows, row => row is { IsHeader: false, Category: FxCategory });
        Assert.Equal("Fx row", fx.Text);
        Assert.Equal(FxCategory, rows.Last(row => row.IsHeader).Category);
        Assert.Same(FxFeature, fx.Entry?.Requires);
    }

    [Fact]
    public async Task ChoosingTheRowCreatesTheNodeThroughTheCreationContext()
    {
        SuggestionRowViewModel fx = Graph.Search.BuildItems(null).Single(row => row is { IsHeader: false, Category: FxCategory });
        Graph.Search.Position = new GraphPoint(10, 20);

        await Graph.Search.SelectCommand.ExecuteAsync(fx);

        Node node = Method.Nodes.OfType<TypeNode>().Single();
        Assert.Equal(10, node.PositionX);
    }

    [Fact]
    public void ACatalogHintNamesAndKeysTheEntry()
    {
        var entry = new TypeEntry("Types", TypeSpecifier.FromType<int>());
        var hint = new NodeHint("Whole Number", "Math", ["integer", "count"]);

        NodeSearchEntry hinted = NodeSearchHints.Apply(entry, new FakeHints(hint));

        Assert.Equal("Whole Number", hinted.Text);
        Assert.Equal("Types", hinted.Category);
        Assert.Equal(["integer", "count"], hinted.Keywords);
        Assert.Contains("integer", SuggestionRowViewModel.Create(hinted).SearchText, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEntryWithNoHintIsUnchanged()
    {
        var entry = new TypeEntry("Types", TypeSpecifier.FromType<int>());

        Assert.Same(entry, NodeSearchHints.Apply(entry, new FakeHints(null)));
        Assert.Same(entry, NodeSearchHints.Apply(entry, null));
    }
}
