using NetPrints.Core;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.NodeSearch;
using NetPrints.Extensibility.Nodes;

namespace NetPrints.Editor.Tests.Search;

/// <summary>The one host-side ranker of the node search (FR-016, FR-017, SC-002, research R10 SUG3).</summary>
public sealed class NodeSearchRankerTests : FixtureGraphTestBase
{
    public NodeSearchRankerTests(IReflectionHost sharedReflection)
        : base(sharedReflection)
    {
    }

    private sealed class ReversingRanker : ISuggestionRanker
    {
        public IEnumerable<NodeSearchEntry> Rank(IEnumerable<NodeSearchEntry> entries) => entries.Reverse();
    }

    private static TypeEntry Entry(string category, string name = "int") =>
        new(category, new TypeSpecifier(name));

    [Fact]
    public void TheDefaultRankerOrdersTheCategoriesInTheFixedOrder()
    {
        string[] shuffled =
        [
            SearchCategories.GenericTypes, SearchCategories.Types, SearchCategories.StaticVariables, SearchCategories.StaticMethods,
            SearchCategories.ThisVariables, SearchCategories.ThisMethods, SearchCategories.PinMethods, SearchCategories.PinVariables,
            SearchCategories.NetPrints,
        ];

        var ranked = DefaultSuggestionRanker.Instance.Rank(shuffled.Select(category => Entry(category))).Select(entry => entry.Category).ToArray();

        Assert.Equal(
            [
                SearchCategories.NetPrints, SearchCategories.PinVariables, SearchCategories.PinMethods, SearchCategories.ThisMethods,
                SearchCategories.ThisVariables, SearchCategories.StaticMethods, SearchCategories.StaticVariables, SearchCategories.Types,
                SearchCategories.GenericTypes,
            ],
            ranked);
    }

    [Fact]
    public void ThisMethodsComeBeforeThisVariables()
    {
        var rows = SuggestionRows.Build(DefaultSuggestionRanker.Instance.Rank(
            [Entry(SearchCategories.ThisVariables, "a"), Entry(SearchCategories.ThisMethods, "b")]));

        Assert.Equal([SearchCategories.ThisMethods, SearchCategories.ThisVariables], rows.Where(row => row.IsHeader).Select(row => row.Category));
    }

    [Fact]
    public void ExtensionCategoriesFollowTheKnownOnesInFirstAppearanceOrder()
    {
        var rows = SuggestionRows.Build(DefaultSuggestionRanker.Instance.Rank(
            [Entry("Zeta", "a"), Entry(SearchCategories.Types, "b"), Entry("Alpha", "c"), Entry(SearchCategories.NetPrints, "d"), Entry("Zeta", "e")]));

        Assert.Equal([SearchCategories.NetPrints, SearchCategories.Types, "Zeta", "Alpha"], rows.Where(row => row.IsHeader).Select(row => row.Category));
    }

    [Fact]
    public void RankingKeepsTheOrderWithinACategory()
    {
        var ranked = DefaultSuggestionRanker.Instance.Rank(
            [Entry(SearchCategories.Types, "b"), Entry(SearchCategories.NetPrints, "x"), Entry(SearchCategories.Types, "a")])
            .OfType<TypeEntry>().Select(entry => entry.Type.FullCodeName).ToArray();

        Assert.Equal(["x", "b", "a"], ranked);
    }

    [Fact]
    public void TheViewModelRanksThroughTheRankerItIsGiven()
    {
        using var search = new NodeSearchViewModel(Graph, NodeSearchProviderRegistry.Default, new ReversingRanker());
        using var standard = new NodeSearchViewModel(Graph);

        var reversed = search.BuildItems(null).Where(row => row.IsHeader).Select(row => row.Category).ToArray();
        var normal = standard.BuildItems(null).Where(row => row.IsHeader).Select(row => row.Category).ToArray();

        Assert.NotEqual(normal, reversed);
    }
}
