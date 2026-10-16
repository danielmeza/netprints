using System.Globalization;
using System.Resources;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Core;
using NetPrints.Editor.Graph.Nodes;
using NetPrints.Editor.Graph.Pins;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.NodeSearch;
using NetPrints.Editor.Tests.Graph;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Extensibility;
using NetPrints.Extensibility.Loading;
using NetPrints.Extensibility.Nodes;
using NetPrints.Graph;

namespace NetPrints.Editor.Tests.Localization;

/// <summary>FR-027, T171: node titles, pin labels, category headers and suggestion names are resolved through the resx, the strings of an extension first.</summary>
public sealed class NodeDisplayTextTests(IReflectionHost sharedReflection)
    : GraphTestBase(TestEditor.Create(_ => sharedReflection, FxHost()))
{
    private const string FxCategory = "Fx Rows";
    private const string SuggestionName = "Say Hello";
    private static readonly CultureInfo German = new("de");

    private static readonly Dictionary<string, string> GermanStrings = new()
    {
        ["Node.ifElse.Title"] = "Wenn Sonst",
        ["Node.ifElse.Pin.out.exec.True"] = "Wahr",
        ["Node.ifElse.Pin.in.data.Condition"] = "Bedingung",
        ["Category.netprints"] = "NetPrints (de)",
        ["Category.fx-rows"] = "Fx Zeilen",
        [SuggestionName] = "Sag Hallo",
    };

    private static ExtensionHost FxHost()
    {
        var manifest = new ExtensionManifest("fx", "Fx", "1.0.0", string.Empty, "1.0", []);
        return new ExtensionHost(new ExtensionLoaderOptions([], [], [BuiltInExtension.InProcessEntry, (manifest, new FxExtension())]), NullLoggerFactory.Instance);
    }

    private static T InCulture<T>(CultureInfo culture, Func<T> read)
    {
        CultureInfo previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = culture;
        try
        {
            return read();
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    private IfElseNode NewIfElse() => new(Method);

    [Fact]
    public void ANodeWithAFixedTitleShowsTheTranslationOfTheExtensionStrings()
    {
        IfElseNode node = NewIfElse();

        Assert.Equal("Wenn Sonst", InCulture(German, () => VmOf(node).Label));
    }

    [Fact]
    public void ANodeWithAFixedTitleShowsTheNeutralTextWithNoTranslation()
    {
        IfElseNode node = NewIfElse();

        Assert.Equal("If Else", InCulture(new CultureInfo("fr"), () => VmOf(node).Label));
        Assert.Equal("If Else", VmOf(node).Label);
    }

    [Fact]
    public void ANodeWhoseTextDependsOnItsContentKeepsItsText()
    {
        var call = new CallMethodNode(Method, ConsoleWriteLine(StringType));

        Assert.Equal(call.ToString(), InCulture(German, () => VmOf(call).Label));
    }

    [Fact]
    public void AShippedPinShowsItsTranslatedLabel()
    {
        IfElseNode node = NewIfElse();

        Assert.Equal("Wahr", InCulture(German, () => VmOf(node.TruePin).DisplayName));
    }

    [Fact]
    public void ADataPinKeepsItsTypeAfterTheTranslatedLabel()
    {
        IfElseNode node = NewIfElse();

        Assert.Equal("Bedingung: Boolean", InCulture(German, () => VmOf(node.ConditionPin).DisplayName));
    }

    [Fact]
    public void APinWithNoResourceShowsItsName()
    {
        IfElseNode node = NewIfElse();

        Assert.Equal("False", InCulture(German, () => VmOf(node.FalsePin).DisplayName));
    }

    [Fact]
    public void ACategoryHeaderShowsTheTranslationAndKeepsItsName()
    {
        List<SuggestionRowViewModel> rows = InCulture(German, () => Graph.Search.BuildItems(null));

        SuggestionRowViewModel netPrints = Assert.Single(rows, row => row is { IsHeader: true, Category: "NetPrints" });
        Assert.Equal("NetPrints (de)", netPrints.Text);
        SuggestionRowViewModel fx = Assert.Single(rows, row => row is { IsHeader: true, Category: FxCategory });
        Assert.Equal("Fx Zeilen", fx.Text);
        Assert.Single(rows, row => row is { IsHeader: true, Category: "This Methods" });
    }

    [Fact]
    public void ASuggestionNameIsTheKeyOfItsTranslation()
    {
        List<SuggestionRowViewModel> rows = InCulture(German, () => Graph.Search.BuildItems(null));

        SuggestionRowViewModel row = Assert.Single(rows, row => row is { IsHeader: false, Category: FxCategory });
        Assert.Equal("Sag Hallo", row.Text);
        Assert.Contains("Sag Hallo", row.SearchText, StringComparison.Ordinal);
    }

    [Fact]
    public void TheNeutralCultureShowsTheSuggestionNameAndTheCategoryAsWritten()
    {
        List<SuggestionRowViewModel> rows = Graph.Search.BuildItems(null);

        Assert.Equal(SuggestionName, Assert.Single(rows, row => row is { IsHeader: false, Category: FxCategory }).Text);
        Assert.Equal("NetPrints", Assert.Single(rows, row => row is { IsHeader: true, Category: "NetPrints" }).Text);
    }

    private sealed class FxExtension : INetPrintsExtension
    {
        public void Register(IExtensionBuilder builder)
        {
            builder.AddStringResources(new GermanResources());
            builder.AddSuggestionProvider(new FxProvider());
        }
    }

    private sealed class FxProvider : INodeSuggestionProvider
    {
        public string Id => "fx.rows";

        public IEnumerable<NodeSearchEntry> Suggest(NodeSearchContext context) =>
            [new NodeKindEntry(FxCategory, new NodeSuggestion(FxCategory, SuggestionName, null, graph => new TypeNode(graph, TypeSpecifier.FromType<int>())))];
    }

    private sealed class GermanResources : ResourceManager
    {
        protected override ResourceSet? InternalGetResourceSet(CultureInfo culture, bool createIfNotExists, bool tryParents) =>
            new FakeSet(culture.TwoLetterISOLanguageName == German.TwoLetterISOLanguageName ? GermanStrings : []);
    }

    private sealed class FakeSet(Dictionary<string, string> entries) : ResourceSet
    {
        public override string? GetString(string name) => entries.GetValueOrDefault(name);

        public override string? GetString(string name, bool ignoreCase) => entries.GetValueOrDefault(name);
    }
}
