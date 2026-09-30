using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NetPrints.Extensibility.Loading;
using Xunit;
using static NetPrints.Tests.Extensibility.ExtensionTestSupport;

namespace NetPrints.Tests.Extensibility.MultiExtension;

/// <summary>MX-T14: fifty extensions with seeded random dependency chains load within the contract's ten seconds, in the same order every time.</summary>
[Collection(nameof(RealExtensionLoadCollection))]
public sealed class ScaleTests : IAsyncLifetime
{
    private const int Count = 50;
    private const int Seed = 20260930;

    private readonly string root = Directory.CreateTempSubdirectory("netprints-scale-").FullName;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        Directory.Delete(root, recursive: true);
        return ValueTask.CompletedTask;
    }

    private static string Id(int index) => $"fx.scale.{index:D2}";

    private string[] BuildFixtures()
    {
        string template = Path.Combine(root, "template");
        Compile(template, "fx.scale", """
            using System;
            using System.IO;
            using NetPrints.Core;
            using NetPrints.Extensibility;
            using NetPrints.Extensibility.Nodes;
            using NetPrints.Graph;
            using NetPrints.Serialization.Documents;
            using NetPrints.Serialization.Mapping;
            using NetPrints.Translator;

            public sealed class ScaleNode : Node { public ScaleNode(NodeGraph graph) : base(graph) { } }
            public sealed record ScaleDoc(string Id, string? Name, System.Collections.Generic.IReadOnlyList<PinStateDocument>? Pins) : NodeDocument(Id, Name, Pins);
            public sealed class Conv(string kind) : INodeDocumentConverter
            {
                public string Kind => kind;
                public Type NodeType => typeof(ScaleNode);
                public Type DocumentType => typeof(ScaleDoc);
                public NodeDocument ToDocument(Node node, NodeMappingContext context) => new ScaleDoc(node.Id, null, null);
                public Node CreateNode(NodeDocument document, NodeGraph graph, NodeMappingContext context) => new ScaleNode(graph);
            }
            public sealed class Translator : INodeTranslator { public void Translate(IExecutionTranslationContext context, Node node, int inputExecPinIndex) { } }
            public sealed class Lib(string id) : INodeLibrary
            {
                public string Id => id;
                public System.Collections.Generic.IReadOnlyList<NodeKindDescriptor> NodeKinds { get; } =
                    [new(id + "/Node", typeof(ScaleNode), new Conv(id + "/Node"), new Translator(), GraphKinds.Method, [])];
            }
            public sealed class Ext : INetPrintsExtension
            {
                public void Register(IExtensionBuilder builder)
                {
                    string id = new DirectoryInfo(Path.GetDirectoryName(typeof(Ext).Assembly.Location) ?? string.Empty).Name;
                    builder.AddNodeLibrary(new Lib(id));
                }
            }
            """);

        var random = new Random(Seed);
        var folders = new string[Count];
        for (int index = 0; index < Count; index++)
        {
            string[] dependsOn = index == 0
                ? []
                : [.. Enumerable.Range(0, random.Next(0, Math.Min(index, 3) + 1)).Select(_ => Id(random.Next(0, index))).Distinct(StringComparer.Ordinal)];
            folders[index] = Path.Combine(root, Id(index));
            Directory.CreateDirectory(folders[index]);
            File.Copy(Path.Combine(template, "fx.scale.dll"), Path.Combine(folders[index], "fx.scale.dll"));
            File.WriteAllText(Path.Combine(folders[index], "netprints-extension.json"), ManifestJson(Id(index), "fx.scale.dll", "1.0", dependsOn));
        }

        return folders;
    }

    [Fact]
    public async Task FiftyExtensionsLoadInUnderTenSecondsAndInTheSameOrderOnTwoRuns()
    {
        string[] folders = BuildFixtures();
        var orders = new List<string[]>();
        var kinds = new List<string[]>();

        for (int run = 0; run < 2; run++)
        {
            var stopwatch = Stopwatch.StartNew();
            await using ExtensionRegistry registry = Load(Options(folders: run == 0 ? folders : folders.AsEnumerable().Reverse()));
            stopwatch.Stop();

            Assert.Empty(registry.Results.OfType<ExtensionLoadResult.Failed>());
            Assert.Empty(registry.Issues);
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10), $"Loading took {stopwatch.Elapsed}.");
            orders.Add([.. registry.Loaded.Select(m => m.Id)]);
            kinds.Add([.. registry.NodeKinds.Select(k => k.Kind)]);
        }

        Assert.Equal(Count, orders[0].Length);
        Assert.Equal(orders[0], orders[1]);
        Assert.Equal(kinds[0], kinds[1]);
        string[] loaded = orders[0];
        foreach (string manifestFolder in folders)
        {
            string manifestPath = Path.Combine(manifestFolder, ExtensionManifest.FileName);
            using FileStream stream = File.OpenRead(manifestPath);
            ExtensionManifest manifest = ExtensionManifest.Parse(stream, manifestPath);
            foreach (string dependency in manifest.DependsOn)
            {
                Assert.True(Array.IndexOf(loaded, dependency) < Array.IndexOf(loaded, manifest.Id), $"{manifest.Id} loaded before {dependency}.");
            }
        }
    }
}
