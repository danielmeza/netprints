using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NetPrints.Core;
using NetPrints.Extensibility;
using NetPrints.Extensibility.Loading;
using NetPrints.Extensibility.Nodes;
using NetPrints.Graph;
using NetPrints.Projects;
using Xunit;
using static NetPrints.Tests.Extensibility.ExtensionTestSupport;

namespace NetPrints.Tests.Extensibility;

/// <summary>FR-021, FR-028, NPXE0006: node search providers an extension contributes reach the registry, with their id rules.</summary>
public class SuggestionProviderTests
{
    private sealed class StubProvider(string id) : INodeSuggestionProvider
    {
        public string Id { get; } = id;

        public IEnumerable<NodeSearchEntry> Suggest(NodeSearchContext context) => [];
    }

    [Fact]
    public async Task AProviderWithTheManifestPrefixIsInTheRegistry()
    {
        var provider = new StubProvider("fx.rows");
        var extension = InProcess("fx", builder => builder.AddSuggestionProvider(provider));

        await using ExtensionRegistry registry = Load(Options([BuiltInExtension.InProcessEntry, extension]));

        Assert.Same(provider, Assert.Single(registry.SuggestionProviders));
        Assert.Empty(registry.Issues);
    }

    [Fact]
    public async Task AProviderIdWithoutTheManifestPrefixIsNpx013AndIsDropped()
    {
        var extension = InProcess("fx", builder => builder.AddSuggestionProvider(new StubProvider("other.rows")));

        await using ExtensionRegistry registry = Load(Options([BuiltInExtension.InProcessEntry, extension]));

        Assert.Empty(registry.SuggestionProviders);
        ExtensionContributionIssue issue = Assert.Single(registry.Issues);
        Assert.Equal(ExtensionDiagnosticCodes.IdPrefix, issue.Code);
        Assert.Equal("NPX013", issue.Code);
        Assert.Equal("fx", issue.ExtensionId);
    }

    [Fact]
    public async Task ADuplicateProviderIdIsNpx004AndTheFirstWins()
    {
        var first = new StubProvider("fx.rows");
        var extension = InProcess("fx", builder => builder.AddSuggestionProvider(first).AddSuggestionProvider(new StubProvider("fx.rows")));

        await using ExtensionRegistry registry = Load(Options([BuiltInExtension.InProcessEntry, extension]));

        Assert.Same(first, Assert.Single(registry.SuggestionProviders));
        ExtensionContributionIssue issue = Assert.Single(registry.Issues);
        Assert.Equal(ExtensionDiagnosticCodes.DuplicateId, issue.Code);
        Assert.Equal("NPX004", issue.Code);
    }

    [Fact]
    public async Task AnEntryRequiresACSharpFeature()
    {
        var feature = new CSharpFeature(new CSharpFeatureId("fx.collections"), new CSharpLanguageVersion(12, 0), ["System.Collections.Generic.List`1"]);
        NodeSearchEntry entry = new RequiringEntry { Requires = feature };

        await Task.CompletedTask;
        Assert.Same(feature, entry.Requires);
        Assert.Equal("fx.collections", entry.Requires.Id.Value);
    }

    private sealed record RequiringEntry() : NodeSearchEntry("Fx", "Row", "icon")
    {
        public override Task<Node?> CreateAsync(INodeCreationContext context, GraphPoint position, NodePin? pin) => Task.FromResult<Node?>(null);
    }
}
