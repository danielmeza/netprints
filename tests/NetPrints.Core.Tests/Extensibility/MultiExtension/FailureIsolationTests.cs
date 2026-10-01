using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NetPrints.Core;
using NetPrints.Extensibility;
using NetPrints.Extensibility.Loading;
using NetPrints.Testing.Extensions;
using Xunit;
using static NetPrints.Tests.Extensibility.ExtensionTestSupport;

namespace NetPrints.Tests.Extensibility.MultiExtension;

/// <summary>MX-T10, MX-T11 and MX-T13: an extension that fails takes nothing of its neighbours with it, and leaves nothing of its own behind.</summary>
[Collection(nameof(RealExtensionLoadCollection))]
public sealed class FailureIsolationTests : IAsyncLifetime
{
    private readonly string root = Directory.CreateTempSubdirectory("netprints-failure-isolation-").FullName;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        Directory.Delete(root, recursive: true);
        return ValueTask.CompletedTask;
    }

    private string Copy(string id, string? folderName = null) => FixtureExtensions.CopyTo(root, id, folderName);

    private static void AssertNeighboursUsable(ExtensionHarness harness)
    {
        Assert.Contains(harness.Registry.Loaded, m => m.Id == FixtureExtensions.Alpha);
        Assert.Contains(harness.Registry.Loaded, m => m.Id == FixtureExtensions.Beta);
        string code = harness.Translate(MultiExtensionGraphs.BuildClass(harness.Registry, "Ns", "Both", "fx.alpha/Ping", "fx.beta/Pong"));
        Assert.Contains("Description(\"fx.alpha\")", code, StringComparison.Ordinal);
        Assert.Contains("Description(\"fx.beta\")", code, StringComparison.Ordinal);
        Assert.Contains("fx.beta pong", code, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARegisterThatThrowsAfterAddingContributionsCommitsNoneOfThemAndFailsWithNpx005()
    {
        var throwing = new DelegateExtension(builder =>
        {
            builder
                .AddNodeLibrary(new SingleKindLibrary("harness.inprocess.0", PingKind("harness.inprocess.0/First"), PongKind("harness.inprocess.0/Second")))
                .AddClassEmitter(new NamedClassEmitter("throws-class"))
                .AddMemberEmitter(new DisposableMemberEmitter())
                .AddProjectProfile(new StubProfile("fx.throws.profile"))
                .AddProjectProperty("FxThrowsProperty");
            throw new InvalidOperationException("midway");
        });

        await using ExtensionHarness harness = await ExtensionHarness.CreateAsync([Copy(FixtureExtensions.Alpha), Copy(FixtureExtensions.Beta)], [throwing], TestContext.Current.CancellationToken);

        ExtensionLoadResult.Failed failed = SingleFailure(harness.Registry, "harness.inprocess.0");
        Assert.Equal("NPX005", failed.Code);
        Assert.Contains("midway", failed.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain(harness.Registry.NodeKinds, k => k.Kind.StartsWith("harness.inprocess.0/", StringComparison.Ordinal));
        Assert.DoesNotContain(harness.Registry.ClassEmitters, e => e.Id == "throws-class");
        Assert.DoesNotContain(harness.Registry.MemberEmitters, e => e is DisposableMemberEmitter);
        Assert.Null(harness.Registry.FindProfile("fx.throws.profile"));
        Assert.DoesNotContain("FxThrowsProperty", harness.Registry.ProjectProperties);
        Assert.Empty(harness.Registry.Issues);
        AssertNeighboursUsable(harness);
    }

    [Fact]
    public async Task ARoslynExtensionThatThrowsMidwayRegistersNoneOfItsKinds()
    {
        string folder = Path.Combine(root, "fx.throws");
        WriteManifest(folder, ManifestJson("fx.throws", "fx.throws.dll"));
        Compile(folder, "fx.throws", """
            using System;
            using NetPrints.Core;
            using NetPrints.Extensibility;
            using NetPrints.Extensibility.Nodes;
            using NetPrints.Graph;
            using NetPrints.Serialization.Documents;
            using NetPrints.Serialization.Mapping;
            using NetPrints.Translator;

            public sealed class ThrowsNode : Node { public ThrowsNode(NodeGraph graph) : base(graph) { } }
            public sealed class OtherNode : Node { public OtherNode(NodeGraph graph) : base(graph) { } }
            public sealed record ThrowsDoc(string Id, string? Name, System.Collections.Generic.IReadOnlyList<PinStateDocument>? Pins) : NodeDocument(Id, Name, Pins);
            public sealed record OtherDoc(string Id, string? Name, System.Collections.Generic.IReadOnlyList<PinStateDocument>? Pins) : NodeDocument(Id, Name, Pins);
            public sealed class Conv<TNode, TDoc>(string kind, Func<NodeGraph, TNode> create, Func<string, TDoc> doc) : INodeDocumentConverter where TNode : Node where TDoc : NodeDocument
            {
                public string Kind => kind;
                public Type NodeType => typeof(TNode);
                public Type DocumentType => typeof(TDoc);
                public NodeDocument ToDocument(Node node, NodeMappingContext context) => doc(node.Id);
                public Node CreateNode(NodeDocument document, NodeGraph graph, NodeMappingContext context) => create(graph);
            }
            public sealed class Translator : INodeTranslator { public void Translate(IExecutionTranslationContext context, Node node, int inputExecPinIndex) { } }
            public sealed class Lib : INodeLibrary
            {
                public string Id => "fx.throws";
                public System.Collections.Generic.IReadOnlyList<NodeKindDescriptor> NodeKinds { get; } =
                [
                    new("fx.throws/One", typeof(ThrowsNode), new Conv<ThrowsNode, ThrowsDoc>("fx.throws/One", g => new ThrowsNode(g), id => new ThrowsDoc(id, null, null)), new Translator(), GraphKinds.Method, []),
                    new("fx.throws/Two", typeof(OtherNode), new Conv<OtherNode, OtherDoc>("fx.throws/Two", g => new OtherNode(g), id => new OtherDoc(id, null, null)), new Translator(), GraphKinds.Method, []),
                ];
            }
            public sealed class Ext : INetPrintsExtension
            {
                public void Register(IExtensionBuilder builder)
                {
                    builder.AddNodeLibrary(new Lib());
                    throw new InvalidOperationException("midway");
                }
            }
            """);

        await using ExtensionHarness harness = await ExtensionHarness.CreateAsync([Copy(FixtureExtensions.Alpha), Copy(FixtureExtensions.Beta), folder], [], TestContext.Current.CancellationToken);

        ExtensionLoadResult.Failed failed = SingleFailure(harness.Registry, "fx.throws");
        Assert.Equal("NPX005", failed.Code);
        Assert.Contains("midway", failed.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain(harness.Registry.NodeKinds, k => k.Kind.StartsWith("fx.throws/", StringComparison.Ordinal));
        AssertNeighboursUsable(harness);
    }

    [Fact]
    public async Task AnExtensionCompiledAgainstANewerHostFailsWithNpx005AndMissingMethodExceptionWhileOthersLoad()
    {
        string folder = Path.Combine(root, "fx.hostskew");
        WriteManifest(folder, ManifestJson("fx.hostskew", "fx.hostskew.dll"));
        CompileAgainstSkewedCore(folder);

        await using ExtensionHarness harness = await ExtensionHarness.CreateAsync([Copy(FixtureExtensions.Alpha), Copy(FixtureExtensions.Beta), folder], [], TestContext.Current.CancellationToken);

        ExtensionLoadResult.Failed failed = SingleFailure(harness.Registry, "fx.hostskew");
        Assert.Equal("NPX005", failed.Code);
        Assert.Contains("MissingMethodException", failed.Reason, StringComparison.Ordinal);
        AssertNeighboursUsable(harness);
    }

    private static void CompileAgainstSkewedCore(string folder)
    {
        string trusted = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty;
        string[] platform = [.. trusted.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)];
        Version version = typeof(ExperimentalApiIds).Assembly.GetName().Version ?? new Version(1, 0, 0, 0);

        string skewedCore = Path.Combine(folder, "skew", "NetPrints.Core.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(skewedCore) ?? folder);
        var coreCompilation = CSharpCompilation.Create(
            "NetPrints.Core",
            [CSharpSyntaxTree.ParseText($$"""
                [assembly: System.Reflection.AssemblyVersion("{{version}}")]
                namespace NetPrints.Core { public static class ExperimentalApiIds { public static void AddedByANewerHost() { } } }
                """)],
            platform.Where(p => !p.EndsWith("NetPrints.Core.dll", StringComparison.Ordinal)).Select(p => MetadataReference.CreateFromFile(p)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        Assert.True(coreCompilation.Emit(skewedCore).Success);

        var extensionCompilation = CSharpCompilation.Create(
            "fx.hostskew",
            [CSharpSyntaxTree.ParseText("""
                using NetPrints.Extensibility;
                public sealed class SkewExtension : INetPrintsExtension
                {
                    public void Register(IExtensionBuilder builder) => NetPrints.Core.ExperimentalApiIds.AddedByANewerHost();
                }
                """)],
            platform.Where(p => !p.EndsWith("NetPrints.Core.dll", StringComparison.Ordinal)).Select(p => MetadataReference.CreateFromFile(p)).Append(MetadataReference.CreateFromFile(skewedCore)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        var emitted = extensionCompilation.Emit(Path.Combine(folder, "fx.hostskew.dll"));
        Assert.True(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
        Directory.Delete(Path.GetDirectoryName(skewedCore) ?? folder, recursive: true);
    }

    public static TheoryData<string> FailureCodes => ["NPX001", "NPX002", "NPX003", "NPX004", "NPX005", "NPX006", "NPX007"];

    [Theory]
    [MemberData(nameof(FailureCodes))]
    public async Task EachFailureCodeLeavesAlphaAndBetaLoadedAndTheRegistryUsable(string code)
    {
        var folders = new List<string> { Copy(FixtureExtensions.Alpha), Copy(FixtureExtensions.Beta) };
        var inProcess = new List<INetPrintsExtension>();
        string failing = Path.Combine(root, "failing");
        switch (code)
        {
            case "NPX001":
                WriteManifest(failing, "{ not json");
                folders.Add(failing);
                break;
            case "NPX002":
                WriteManifest(failing, ManifestJson("fx.future", "fx.future.dll", api: "9.0"));
                folders.Add(failing);
                break;
            case "NPX003":
                WriteManifest(failing, ManifestJson("fx.dependent", "fx.dependent.dll", "1.0", "fx.absent"));
                folders.Add(failing);
                break;
            case "NPX004":
                folders.Add(Copy(FixtureExtensions.Alpha, "fx.alpha-second"));
                break;
            case "NPX005":
                inProcess.Add(new DelegateExtension(_ => throw new InvalidOperationException("boom")));
                break;
            case "NPX006":
                inProcess.Add(new DelegateExtension(builder => builder.AddProjectProfile(new StubProfile("fx.alpha.profile"))));
                break;
            default:
                WriteManifest(failing, ManifestJson("fx.hollow", "fx.hollow.dll"));
                folders.Add(failing);
                break;
        }

        await using ExtensionHarness harness = await ExtensionHarness.CreateAsync(folders, inProcess, TestContext.Current.CancellationToken);

        if (code == "NPX006")
        {
            Assert.Equal(code, Assert.Single(harness.Registry.Issues).Code);
            Assert.Empty(harness.Registry.Results.OfType<ExtensionLoadResult.Failed>());
        }
        else
        {
            Assert.Equal(code, Assert.Single(harness.Registry.Results.OfType<ExtensionLoadResult.Failed>()).Code);
        }

        AssertNeighboursUsable(harness);
    }
}
