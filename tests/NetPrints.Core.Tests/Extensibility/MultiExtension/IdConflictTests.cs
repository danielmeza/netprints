using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using NetPrints.Catalog;
using NetPrints.Core;
using NetPrints.Extensibility;
using NetPrints.Extensibility.Hosting;
using NetPrints.Extensibility.Loading;
using NetPrints.Extensibility.Nodes;
using NetPrints.Extensibility.Settings;
using NetPrints.Graph;
using NetPrints.Serialization;
using NetPrints.Serialization.Documents;
using NetPrints.Serialization.Json;
using NetPrints.Serialization.Mapping;
using NetPrints.Testing.Extensions;
using Xunit;
using static NetPrints.Tests.Extensibility.ExtensionTestSupport;

namespace NetPrints.Tests.Extensibility.MultiExtension;

/// <summary>MX-T07 and MX-T08: a later extension cannot take what alpha registered, and a duplicate id loses to the first folder (ADR-0010 §4).</summary>
[Collection(nameof(RealExtensionLoadCollection))]
public sealed class IdConflictTests : IAsyncLifetime
{
    private const string Squatter = "fx.squatter";

    private readonly string root = Directory.CreateTempSubdirectory("netprints-id-conflict-").FullName;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        Directory.Delete(root, recursive: true);
        return ValueTask.CompletedTask;
    }

    private Assembly LoadedAlphaAssembly() =>
        AssemblyLoadContext.All
            .Where(context => context.Name == FixtureExtensions.Alpha)
            .SelectMany(context => context.Assemblies)
            .Single(assembly => assembly.GetName().Name == "Fx.Alpha" && assembly.Location.StartsWith(root, StringComparison.Ordinal));

    private (ExtensionManifest, INetPrintsExtension) SquatterExtension() => InProcess(Squatter, builder =>
    {
        Assembly alpha = LoadedAlphaAssembly();
        Type alphaNode = alpha.GetType("Fx.Alpha.AlphaPingNode") ?? throw new InvalidOperationException("AlphaPingNode not found.");
        Type alphaDocument = alpha.GetType("Fx.Kit.FxNodeDocument") ?? throw new InvalidOperationException("FxNodeDocument not found.");
        builder
            .AddNodeLibrary(new SingleKindLibrary(
                Squatter,
                Kind("fx.alpha/Ping", typeof(PongNode), typeof(PongNodeDocument)),
                Kind("fx.squatter/SameClrType", alphaNode, typeof(PingNodeDocument)),
                Kind("fx.squatter/SameDocumentType", typeof(PongNode), alphaDocument)))
            .AddJsonTypeInfoResolver(new DefaultJsonTypeInfoResolver())
            .AddProjectProfile(new StubProfile("fx.alpha.profile"))
            .AddHostChannel(new StubChannelFactory("fx.alpha.channel"))
            .AddSettings(NetPrintsSettings.Descriptor with { ExtensionId = FixtureExtensions.Alpha })
            .AddCatalogProfile(new CatalogProfile("fx-alpha", CatalogProfileBase.None))
            .AddProjectProperty("FXALPHAPROPERTY");
    }, "1.0", FixtureExtensions.Alpha);

    private static NodeKindDescriptor Kind(string kind, Type nodeType, Type documentType) =>
        new(kind, nodeType, new ShapeConverter(kind, nodeType, documentType), new PingTranslator(), GraphKinds.Method, []);

    [Fact]
    public async Task ASquatterCannotTakeAnyIdAlphaRegisteredAndAlphaIsUnchanged()
    {
        string alphaFolder = FixtureExtensions.CopyTo(root, FixtureExtensions.Alpha);

        await using ExtensionRegistry registry = Load(Options(inProcess: [SquatterExtension()], folders: [alphaFolder]));

        Assert.Contains(Squatter, registry.Loaded.Select(m => m.Id));
        string[] rejected = [.. registry.Issues.Select(issue => issue.Contribution)];
        Assert.All(registry.Issues, issue =>
        {
            Assert.Equal(Squatter, issue.ExtensionId);
            Assert.Equal("NPX006", issue.Code);
        });
        string[] expected =
        [
            "node kind fx.alpha/Ping",
            "node kind fx.squatter/SameClrType",
            "node kind fx.squatter/SameDocumentType",
            "profile fx.alpha.profile",
            "host channel fx.alpha.channel",
            "settings fx.alpha",
            "catalog profile fx-alpha",
            "JSON resolver",
        ];
        Assert.Equal(expected.Order(StringComparer.Ordinal), rejected.Order(StringComparer.Ordinal));

        NodeKindDescriptor ping = Assert.Single(registry.NodeKinds, k => k.Kind.StartsWith("fx.", StringComparison.Ordinal));
        Assert.Equal("fx.alpha/Ping", ping.Kind);
        Assert.Equal("AlphaPingNode", ping.NodeType.Name);
        Assert.Equal("AlphaProfile", registry.FindProfile("fx.alpha.profile")?.GetType().Name);
        Assert.Equal("AlphaHostChannelFactory", registry.FindHostChannel("fx.alpha.channel")?.GetType().Name);
        ExtensionSettingsDescriptor settings = Assert.Single(registry.Settings, s => s.ExtensionId == FixtureExtensions.Alpha);
        Assert.Equal("AlphaSettings", settings.ValueType.Name);
        Assert.Equal("fx-alpha", Assert.Single(registry.CatalogProfiles).Id);
        Assert.Equal(CatalogProfileBase.PublicApi, registry.CatalogProfiles[0].Base);
    }

    [Fact]
    public async Task AProjectPropertyTwiceRegisteredIgnoringCaseIsKeptOnceWithoutAnIssue()
    {
        string alphaFolder = FixtureExtensions.CopyTo(root, FixtureExtensions.Alpha);

        await using ExtensionRegistry registry = Load(Options(inProcess: [SquatterExtension()], folders: [alphaFolder]));

        Assert.Equal("FxAlphaProperty", Assert.Single(registry.ProjectProperties));
        Assert.DoesNotContain(registry.Issues, issue => issue.Contribution.Contains("propert", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ASquattersCatchAllJsonResolverIsReportedAndAlphasDocumentsStillComeFromAlphasResolver()
    {
        string alphaFolder = FixtureExtensions.CopyTo(root, FixtureExtensions.Alpha);

        await using ExtensionRegistry registry = Load(Options(inProcess: [SquatterExtension()], folders: [alphaFolder]));

        ExtensionContributionIssue issue = Assert.Single(registry.Issues, i => i.Contribution.Contains("JSON resolver", StringComparison.Ordinal));
        Assert.Equal(Squatter, issue.ExtensionId);
        Assert.Equal(ExtensionDiagnosticCodes.ContributionRejected, issue.Code);
        Assert.Contains(FixtureExtensions.Alpha, issue.Reason, StringComparison.Ordinal);

        Type alphaDocument = LoadedAlphaAssembly().GetType("Fx.Kit.FxNodeDocument") ?? throw new InvalidOperationException("FxNodeDocument not found.");
        JsonTypeInfo typeInfo = new NetPrintsJsonOptions(registry.NodeConverters).SerializerOptions.GetTypeInfo(alphaDocument);
        Assert.IsAssignableFrom<JsonSerializerContext>(typeInfo.OriginatingResolver);
    }

    [Fact]
    public async Task AResolverThatThrowsWhenProbedIsReportedAndTheOtherExtensionsStillLoad()
    {
        string alphaFolder = FixtureExtensions.CopyTo(root, FixtureExtensions.Alpha);
        (ExtensionManifest, INetPrintsExtension) thrower = InProcess("fx.thrower", builder => builder.AddJsonTypeInfoResolver(new ThrowingResolver()), "1.0");

        await using ExtensionRegistry registry = Load(Options(inProcess: [thrower], folders: [alphaFolder]));

        Assert.Contains(FixtureExtensions.Alpha, registry.Loaded.Select(m => m.Id));
        Assert.Contains("fx.thrower", registry.Loaded.Select(m => m.Id));
        Assert.Contains(registry.NodeKinds, k => k.Kind == "fx.alpha/Ping");
        ExtensionContributionIssue issue = Assert.Single(registry.Issues);
        Assert.Equal("fx.thrower", issue.ExtensionId);
        Assert.Equal(ExtensionDiagnosticCodes.ContributionRejected, issue.Code);
        Assert.Equal("JSON resolver", issue.Contribution);
        Assert.Contains(nameof(ArgumentException), issue.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AlphasDocumentsRoundTripThroughAlphasResolverWhileASquattersCatchAllResolverIsPresent()
    {
        string alphaFolder = FixtureExtensions.CopyTo(root, FixtureExtensions.Alpha);

        await using ExtensionRegistry registry = Load(Options(inProcess: [BuiltInExtension.InProcessEntry, SquatterExtension()], folders: [alphaFolder]));
        byte[] document = await MultiExtensionGraphs.WriteAsync(registry, MultiExtensionGraphs.BuildClass(registry, "Ns", "OnlyAlpha", "fx.alpha/Ping"));
        JsonDocumentFormat format = ExtensionGraphs.Format(registry);
        await using var input = new MemoryStream(document);
        ClassDocument read = await format.ReadClassAsync(input, new DocumentId("alpha-only"), TestContext.Current.CancellationToken);
        await using var output = new MemoryStream();
        await format.WriteClassAsync(read, output, TestContext.Current.CancellationToken);
        byte[] saved = output.ToArray();

        Assert.Equal(document, saved);
    }

    [Fact]
    public async Task ADuplicateIdFailsWithNpx004AndTheFirstFolderWinsInEveryDiscoveryOrder()
    {
        string first = FixtureExtensions.CopyTo(root, FixtureExtensions.Alpha, "fx.alpha-a");
        string second = FixtureExtensions.CopyTo(root, FixtureExtensions.Alpha, "fx.alpha-b");

        foreach ((string winner, string loser) in new[] { (first, second), (second, first) })
        {
            await using ExtensionHarness harness = await ExtensionHarness.CreateAsync([winner, loser], [], TestContext.Current.CancellationToken);

            ExtensionLoadResult.Loaded loaded = Assert.Single(harness.Registry.Results.OfType<ExtensionLoadResult.Loaded>(), r => r.Id == FixtureExtensions.Alpha);
            Assert.Equal(Path.Combine(winner, ExtensionManifest.FileName), loaded.ManifestPath);
            ExtensionLoadResult.Failed failed = SingleFailure(harness.Registry, FixtureExtensions.Alpha);
            Assert.Equal("NPX004", failed.Code);
            Assert.Equal(Path.Combine(loser, ExtensionManifest.FileName), failed.ManifestPath);
            Assert.Single(harness.Registry.NodeKinds, k => k.Kind == "fx.alpha/Ping");
            Assert.Single(harness.Registry.CatalogProfiles);
        }
    }

    private sealed class StubChannelFactory(string id) : IHostChannelFactory
    {
        public string Id => id;

        public IHostChannel Create(HostLaunchContext context) => NullHostChannel.Instance;
    }

    private sealed class ThrowingResolver : IJsonTypeInfoResolver
    {
        public JsonTypeInfo? GetTypeInfo(Type type, System.Text.Json.JsonSerializerOptions options) => throw new ArgumentException("boom");
    }

    private sealed class ShapeConverter(string kind, Type nodeType, Type documentType) : INodeDocumentConverter
    {
        public string Kind => kind;

        public Type NodeType => nodeType;

        public Type DocumentType => documentType;

        public NodeDocument ToDocument(Node node, NodeMappingContext context) => throw new NotSupportedException();

        public Node CreateNode(NodeDocument document, NodeGraph graph, NodeMappingContext context) => throw new NotSupportedException();
    }
}
