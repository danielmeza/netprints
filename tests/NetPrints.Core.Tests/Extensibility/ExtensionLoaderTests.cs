using System;
using System.IO;
using System.Linq;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NetPrints.Core;
using NetPrints.Extensibility;
using NetPrints.Extensibility.Loading;
using NetPrints.Extensibility.Nodes;
using NetPrints.Graph;
using Xunit;
using static NetPrints.Tests.Extensibility.ExtensionTestSupport;

namespace NetPrints.Tests.Extensibility;

/// <summary>Discovery, validation, ordering and loading (extension-points.md §8.1, EX-T10, EX-T11).</summary>
[Collection(nameof(RealExtensionLoadCollection))]
public class ExtensionLoaderTests : IDisposable
{
    private readonly string root = NewTempDirectory();

    public void Dispose() => Directory.Delete(root, recursive: true);

    private static string Sample(string id, string api = "1.0", params string[] dependsOn) =>
        ManifestJson(id, api: api, dependsOn: dependsOn);

    [Fact]
    public async Task SearchDirectoriesContributeTheirImmediateSubfoldersInOrdinalOrder()
    {
        WriteManifest(Path.Combine(root, "b-folder"), Sample("test.b"));
        WriteManifest(Path.Combine(root, "a-folder"), Sample("test.a"));
        WriteManifest(Path.Combine(root, "no-manifest"), string.Empty);
        File.Delete(Path.Combine(root, "no-manifest", ExtensionManifest.FileName));
        WriteManifest(Path.Combine(root, "a-folder", "nested"), Sample("test.nested"));
        var logs = new CollectingLoggerFactory();

        await using ExtensionRegistry registry = Load(Options(searchDirectories: [Path.Combine(root, "missing"), root]), logs);

        // Every assembly is missing (NPX007), so each is reported as a failure in discovery order.
        Assert.Equal(["test.a", "test.b"], registry.Results.Select(r => r.Id));
        Assert.All(registry.Results, r => Assert.Equal("NPX007", Assert.IsType<ExtensionLoadResult.Failed>(r).Code));
        Assert.Equal(2, logs.Entries.Count(e => e.EventId.Id == 2001));
        Assert.Contains(logs.Entries, e => e.EventId.Id == 2008);
    }

    [Fact]
    public async Task AnExplicitFolderWithoutAManifestFailsWithNpx001()
    {
        string folder = Path.Combine(root, "empty");
        Directory.CreateDirectory(folder);

        await using ExtensionRegistry registry = Load(Options(folders: [folder]));

        var failure = SingleFailure(registry, "empty");
        Assert.Equal("NPX001", failure.Code);
        Assert.Null(failure.Exception as InvalidOperationException);
    }

    [Fact]
    public async Task AnInvalidManifestFailsWithNpx001AndTheOtherExtensionStillLoads()
    {
        string bad = Path.GetDirectoryName(WriteManifest(Path.Combine(root, "bad"), "{ nope")) ?? root;
        var logs = new CollectingLoggerFactory();
        var good = InProcess("test.good", builder => builder.AddClassEmitter(new NamedClassEmitter("good")));

        await using ExtensionRegistry registry = Load(Options([good], [bad]), logs);

        var failure = SingleFailure(registry, "bad");
        Assert.Equal("NPX001", failure.Code);
        Assert.EndsWith(ExtensionManifest.FileName, failure.ManifestPath);
        Assert.IsType<ExtensionManifestException>(failure.Exception);
        Assert.Equal(["test.good"], registry.Loaded.Select(m => m.Id));
        Assert.Single(registry.ClassEmitters);
        Assert.Contains(logs.Entries, e => e.EventId.Id == 2003 && e.Level == LogLevel.Error);
    }

    [Theory]
    [InlineData("2.0")]
    [InlineData("0.9")]
    [InlineData("1.1")]
    public async Task AnIncompatibleApiVersionFailsWithNpx002(string api)
    {
        string folder = Path.GetDirectoryName(WriteManifest(Path.Combine(root, "x"), Sample("test.x", api))) ?? root;

        await using ExtensionRegistry registry = Load(Options(folders: [folder]));

        var failure = SingleFailure(registry, "test.x");
        Assert.Equal("NPX002", failure.Code);
        Assert.Contains(api, failure.Reason);
    }

    [Fact]
    public async Task ACompatibleApiVersionPassesValidation()
    {
        string folder = Path.GetDirectoryName(WriteManifest(Path.Combine(root, "x"), Sample("test.x", "1.0"))) ?? root;

        await using ExtensionRegistry registry = Load(Options(folders: [folder]));

        Assert.Equal("NPX007", SingleFailure(registry, "test.x").Code);
    }

    [Fact]
    public async Task AMissingDependencyFailsWithNpx003()
    {
        var extension = InProcess("test.ext", _ => { }, dependsOn: "test.absent");

        await using ExtensionRegistry registry = Load(Options([extension]));

        var failure = SingleFailure(registry, "test.ext");
        Assert.Equal("NPX003", failure.Code);
        Assert.Contains("test.absent", failure.Reason);
    }

    [Fact]
    public async Task ADependencyThatFailedMakesTheDependentFailWithNpx003()
    {
        var broken = InProcess("test.broken", _ => throw new InvalidOperationException("boom"));
        var dependent = InProcess("test.dependent", _ => { }, dependsOn: "test.broken");

        await using ExtensionRegistry registry = Load(Options([broken, dependent]));

        Assert.Equal("NPX005", SingleFailure(registry, "test.broken").Code);
        Assert.Equal("NPX003", SingleFailure(registry, "test.dependent").Code);
        Assert.Empty(registry.Loaded);
    }

    [Fact]
    public async Task ADependencyCycleFailsEveryMemberWithNpx003()
    {
        var a = InProcess("test.a", _ => { }, dependsOn: "test.b");
        var b = InProcess("test.b", _ => { }, dependsOn: "test.a");
        var self = InProcess("test.self", _ => { }, dependsOn: "test.self");
        var fine = InProcess("test.fine", _ => { });

        await using ExtensionRegistry registry = Load(Options([a, b, self, fine]));

        Assert.Equal("NPX003", SingleFailure(registry, "test.a").Code);
        Assert.Equal("NPX003", SingleFailure(registry, "test.b").Code);
        Assert.Equal("NPX003", SingleFailure(registry, "test.self").Code);
        Assert.Equal(["test.fine"], registry.Loaded.Select(m => m.Id));
    }

    [Fact]
    public async Task ADuplicateIdFailsWithNpx004AndTheFirstWins()
    {
        var first = InProcess("test.dup", builder => builder.AddClassEmitter(new NamedClassEmitter("first")));
        string folder = Path.GetDirectoryName(WriteManifest(Path.Combine(root, "dup"), Sample("test.dup"))) ?? root;
        var logs = new CollectingLoggerFactory();

        await using ExtensionRegistry registry = Load(Options([first], [folder]), logs);

        Assert.Equal("first", Assert.Single(registry.ClassEmitters).Id);
        Assert.Equal("NPX004", SingleFailure(registry, "test.dup").Code);
        Assert.Single(registry.Loaded);
        Assert.Contains(logs.Entries, e => e.EventId.Id == 2004 && e.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task TheSameFolderListedTwiceIsLoadedOnce()
    {
        string folder = Path.GetDirectoryName(WriteManifest(Path.Combine(root, "x"), Sample("test.x"))) ?? root;

        await using ExtensionRegistry registry = Load(Options(folders: [folder, folder], searchDirectories: [root]));

        Assert.Single(registry.Results);
    }

    [Fact]
    public async Task AMissingAssemblyFailsWithNpx007()
    {
        string folder = Path.GetDirectoryName(WriteManifest(Path.Combine(root, "x"), Sample("test.x"))) ?? root;

        await using ExtensionRegistry registry = Load(Options(folders: [folder]));

        Assert.Equal("NPX007", SingleFailure(registry, "test.x").Code);
    }

    [Fact]
    public async Task AnAssemblyThatIsNotManagedCodeFailsWithNpx007()
    {
        string folder = Path.Combine(root, "x");
        WriteManifest(folder, ManifestJson("test.x", "x.dll"));
        File.WriteAllText(Path.Combine(folder, "x.dll"), "not an assembly");

        await using ExtensionRegistry registry = Load(Options(folders: [folder]));

        var failure = SingleFailure(registry, "test.x");
        Assert.Equal("NPX007", failure.Code);
        Assert.NotNull(failure.Exception);
    }

    [Fact]
    public async Task AnAssemblyWithoutAnExtensionTypeFailsWithNpx001()
    {
        string folder = Path.Combine(root, "x");
        WriteManifest(folder, ManifestJson("test.x", "NetPrints.Serialization.dll"));
        File.Copy(Path.Combine(AppContext.BaseDirectory, "NetPrints.Serialization.dll"), Path.Combine(folder, "NetPrints.Serialization.dll"));

        await using ExtensionRegistry registry = Load(Options(folders: [folder]));

        var failure = SingleFailure(registry, "test.x");
        Assert.Equal("NPX001", failure.Code);
        Assert.Contains("no public INetPrintsExtension", failure.Reason);
    }

    [Fact]
    public async Task AnAssemblyWithTwoExtensionTypesFailsWithNpx001()
    {
        string folder = Path.Combine(root, "x");
        WriteManifest(folder, ManifestJson("test.x", "Two.dll"));
        Compile(folder, "Two", """
            using NetPrints.Extensibility;
            public class One : INetPrintsExtension { public void Register(IExtensionBuilder builder) { } }
            public class Two : INetPrintsExtension { public void Register(IExtensionBuilder builder) { } }
            """);

        await using ExtensionRegistry registry = Load(Options(folders: [folder]));

        var failure = SingleFailure(registry, "test.x");
        Assert.Equal("NPX001", failure.Code);
        Assert.Contains("2 INetPrintsExtension", failure.Reason);
    }

    [Fact]
    public async Task AnExtensionWhoseConstructorThrowsFailsWithNpx005()
    {
        string folder = Path.Combine(root, "x");
        WriteManifest(folder, ManifestJson("test.x", "Throwing.dll"));
        Compile(folder, "Throwing", """
            using NetPrints.Extensibility;
            public class Ext : INetPrintsExtension
            {
                public Ext() { throw new System.InvalidOperationException("ctor boom"); }
                public void Register(IExtensionBuilder builder) { }
            }
            """);

        await using ExtensionRegistry registry = Load(Options(folders: [folder]));

        var failure = SingleFailure(registry, "test.x");
        Assert.Equal("NPX005", failure.Code);
        Assert.Contains("ctor boom", failure.Reason);
    }

    [Fact]
    public async Task AnExtensionAssemblyLoadsInItsOwnContextAndSharesNetPrintsTypes()
    {
        string folder = Path.Combine(root, "x");
        WriteManifest(folder, ManifestJson("test.x", "Sample.dll"));
        Compile(folder, "Sample", """
            using NetPrints.Extensibility;
            public class Ext : INetPrintsExtension
            {
                public static System.Type NodeTypeSeenByTheExtension => typeof(NetPrints.Graph.Node);
                public void Register(IExtensionBuilder builder) => builder.AddClassEmitter(new Emitter());
            }
            public class Emitter : NetPrints.Translator.IClassEmitter
            {
                public string Id => "sample";
                public void EmitClass(NetPrints.Translator.ClassEmitContext context) { }
            }
            """);
        var logs = new CollectingLoggerFactory();

        await using ExtensionRegistry registry = Load(Options(folders: [folder]), logs);

        var loaded = Assert.IsType<ExtensionLoadResult.Loaded>(Assert.Single(registry.Results));
        Assert.Equal("test.x", loaded.Manifest.Id);
        Assert.EndsWith(ExtensionManifest.FileName, loaded.ManifestPath);
        var emitter = Assert.Single(registry.ClassEmitters);
        var assembly = emitter.GetType().Assembly;
        var context = AssemblyLoadContext.GetLoadContext(assembly);
        Assert.NotNull(context);
        Assert.NotSame(AssemblyLoadContext.Default, context);
        Assert.Equal("test.x", context.Name);
        Assert.False(context.IsCollectible);
        var seen = (Type?)assembly.GetType("Ext")?.GetProperty("NodeTypeSeenByTheExtension")?.GetValue(null);
        Assert.Same(typeof(Node), seen);
        Assert.Contains(logs.Entries, e => e.EventId.Id == 2002 && e.Message == "Loaded extension test.x 1.0.0");
    }

    [Fact]
    public async Task ExtensionsLoadDependenciesFirstThenByIdAndInProcessOnesAhead()
    {
        var order = new System.Collections.Generic.List<string>();
        (ExtensionManifest, INetPrintsExtension) Ext(string id, params string[] dependsOn) =>
            InProcess(id, builder => order.Add(builder.Manifest.Id), dependsOn: dependsOn);
        string folder = Path.Combine(root, "search");
        WriteManifest(Path.Combine(folder, "1"), ManifestJson("test.zeta", "z.dll", dependsOn: "test.alpha"));
        WriteManifest(Path.Combine(folder, "2"), ManifestJson("test.alpha", "a.dll"));
        WriteManifest(Path.Combine(folder, "3"), ManifestJson("test.mid", "m.dll"));
        var inProcess = new[] { Ext("test.late", "test.early"), Ext("test.early") };

        await using ExtensionRegistry registry = Load(Options(inProcess, searchDirectories: [folder]));

        Assert.Equal(["test.early", "test.late"], order);
        Assert.Equal(["test.early", "test.late"], registry.Loaded.Select(m => m.Id));
        Assert.Equal(["test.early", "test.late", "test.alpha", "test.mid", "test.zeta"], registry.Results.Select(r => r.Id));
    }

    [Fact]
    public async Task TiesAreBrokenByOrdinalId()
    {
        string folder = Path.Combine(root, "search");
        foreach (string id in new[] { "test.c", "test.a", "test.B", "test.b" })
        {
            WriteManifest(Path.Combine(folder, id), ManifestJson(id, id + ".dll"));
        }

        await using ExtensionRegistry registry = Load(Options(searchDirectories: [folder]));

        Assert.Equal(["test.B", "test.a", "test.b", "test.c"], registry.Results.Select(r => r.Id));
    }

    [Fact]
    public async Task EmittersAreAppliedInLoadOrder()
    {
        var late = InProcess("test.late", builder => builder.AddClassEmitter(new NamedClassEmitter("late")), dependsOn: "test.early");
        var early = InProcess("test.early", builder => builder.AddClassEmitter(new NamedClassEmitter("early")));

        await using ExtensionRegistry registry = Load(Options([late, early]));

        Assert.Equal(["early", "late"], registry.ClassEmitters.Select(e => e.Id));
        Assert.Equal(["early", "late"], registry.Translation.ClassEmitters.Select(e => e.Id));
    }

    [Fact]
    public async Task ADuplicateKindOrNodeTypeRejectsOnlyTheLaterKindAndKeepsTheExtension()
    {
        var first = InProcess("test.one", builder => builder.AddNodeLibrary(new SingleKindLibrary("test.one", PingKind("test.one/Ping"))));
        var second = InProcess("test.two", builder => builder
            .AddNodeLibrary(new SingleKindLibrary("test.two", PingKind("test.two/Ping"), PongKind("test.two/Pong")))
            .AddNodeLibrary(new SingleKindLibrary("test.two/more", PongKind("test.two/Pong"))));
        var logs = new CollectingLoggerFactory();

        await using ExtensionRegistry registry = Load(Options([first, second]), logs);

        Assert.Equal(["test.one", "test.two"], registry.Loaded.Select(m => m.Id));
        Assert.Equal(["test.one/Ping", "test.two/Pong"], registry.NodeKinds.Select(k => k.Kind));
        Assert.Equal(["node kind test.two/Ping", "node kind test.two/Pong"], registry.Issues.Select(i => i.Contribution));
        Assert.All(registry.Issues, issue => Assert.Equal("test.two", issue.ExtensionId));
        Assert.All(registry.Issues, issue => Assert.Equal("NPX006", issue.Code));
        Assert.Equal(2, logs.Entries.Count(e => e.EventId.Id == 2005 && e.Level == LogLevel.Error));
    }

    [Fact]
    public async Task InconsistentNodeKindsAreRejectedWithNpx006()
    {
        var wrongPrefix = PingKind("other.ext/Ping");
        var mismatch = PingKind("test.ext/Mismatch") with { Converter = new PingConverter("test.ext/Other") };
        var builtInName = PingKind("ifElse");
        var notANode = PingKind("test.ext/NotANode") with { NodeType = typeof(string) };
        var extension = InProcess("test.ext", builder => builder.AddNodeLibrary(new SingleKindLibrary(
            "test.ext", wrongPrefix, mismatch, builtInName, notANode)));

        await using ExtensionRegistry registry = Load(Options([extension]));

        Assert.Equal("test.ext", Assert.Single(registry.Loaded).Id);
        Assert.Empty(registry.NodeKinds);
        Assert.Equal(4, registry.Issues.Count);
        Assert.All(registry.Issues, i => Assert.Equal("NPX006", i.Code));
    }

    [Fact]
    public async Task AKindForAnAlreadyRegisteredNodeTypeIsRejected()
    {
        var extension = InProcess("test.ext", builder => builder.AddNodeLibrary(new SingleKindLibrary(
            "test.ext", PingKind("test.ext/A"), PingKind("test.ext/B"))));

        await using ExtensionRegistry registry = Load(Options([extension]));

        Assert.Equal("test.ext/A", Assert.Single(registry.NodeKinds).Kind);
        Assert.Equal("node kind test.ext/B", Assert.Single(registry.Issues).Contribution);
    }

    [Fact]
    public async Task ProfileConflictsAreRejectedWithNpx006()
    {
        var one = InProcess("test.one", builder => builder.AddProjectProfile(new StubProfile("test.profile")));
        var two = InProcess("test.two", builder => builder
            .AddProjectProfile(new StubProfile("test.profile"))
            .AddProjectProfile(new StubProfile(DefaultProjectProfile.ProfileId)));
        var logs = new CollectingLoggerFactory();

        await using ExtensionRegistry registry = Load(Options([one, two]), logs);

        Assert.Equal(["test.two", "test.two"], registry.Issues.Select(i => i.ExtensionId));
        Assert.Same(DefaultProjectProfile.Instance, registry.FindProfile(DefaultProjectProfile.ProfileId));
        Assert.Equal(2, registry.Profiles.Count);
        Assert.Equal(2, logs.Entries.Count(e => e.EventId.Id == 2007));
    }

    [Fact]
    public async Task TheTestExtensionLoadsFromASearchDirectoryInItsOwnContextWithEveryContribution()
    {
        string search = Path.Combine(root, "search");
        TestExtensionLocation.CopyTo(search);
        var logs = new CollectingLoggerFactory();

        await using ExtensionRegistry registry = Load(Options(searchDirectories: [search]), logs);

        Assert.Equal(["netprints.test"], registry.Loaded.Select(m => m.Id));
        Assert.Empty(registry.Issues);
        NodeKindDescriptor kind = Assert.Single(registry.NodeKinds);
        Assert.Equal("netprints.test/Log", kind.Kind);

        var assembly = kind.NodeType.Assembly;
        var context = AssemblyLoadContext.GetLoadContext(assembly);
        Assert.NotNull(context);
        Assert.NotSame(AssemblyLoadContext.Default, context);
        Assert.Equal("netprints.test", context.Name);
        Assert.StartsWith(search, assembly.Location, StringComparison.Ordinal);
        Assert.Same(typeof(Node), kind.NodeType.BaseType);
        Assert.Same(typeof(Node).Assembly, kind.NodeType.BaseType?.Assembly);

        Assert.Equal(["netprints.test/class"], registry.ClassEmitters.Select(e => e.Id));
        Assert.Equal(["netprints.test/member"], registry.MemberEmitters.Select(e => e.Id));
        Assert.Equal("netprints.test/catalog", Assert.Single(registry.TypeCatalogs).Info.Id);
        Assert.NotNull(registry.FindProfile("netprints.test"));
        Assert.Equal(["netprints.test"], registry.Settings.Select(d => d.ExtensionId));
        Assert.NotNull(registry.FindHostChannel("test"));
        Assert.Contains("NetPrintsTestMode", registry.ProjectProperties);
        Assert.Single(registry.JsonTypeInfoResolvers);
        Assert.Contains(logs.Entries, e => e.EventId.Id == 2002 && e.Message == "Loaded extension netprints.test 1.0.0");
    }

    [Fact]
    public async Task TheTestExtensionLoadsFromAnExplicitFolderAndItsTranslatorJoinsTheEnvironment()
    {
        await using ExtensionRegistry registry = Load(Options(folders: [TestExtensionLocation.Folder]));

        Assert.Equal(["netprints.test"], registry.Loaded.Select(m => m.Id));
        NodeKindDescriptor kind = Assert.Single(registry.NodeKinds);
        Assert.Same(kind.Translator, registry.Translation.Nodes.Find(kind.NodeType));
        Assert.Same(kind.Converter, registry.NodeConverters.FindByDocumentType(kind.Converter.DocumentType));
    }

    [Fact]
    public async Task AFailingSiblingLeavesTheTestExtensionLoadedAndTheRegistryUsable()
    {
        string search = Path.Combine(root, "search");
        TestExtensionLocation.CopyTo(search);
        WriteManifest(Path.Combine(search, "future"), Sample("test.future", api: "2.0"));
        WriteManifest(Path.Combine(search, "gone"), ManifestJson("test.gone", "missing.dll"));

        await using ExtensionRegistry registry = Load(Options(searchDirectories: [search]));

        Assert.Equal(["netprints.test"], registry.Loaded.Select(m => m.Id));
        Assert.Equal("NPX002", SingleFailure(registry, "test.future").Code);
        Assert.Equal("NPX007", SingleFailure(registry, "test.gone").Code);
        Assert.Single(registry.NodeKinds);
    }

    [Fact]
    public async Task ADependentOfTheTestExtensionLoadsAfterItAndItsEmittersRunAfterTheTestEmitters()
    {
        string search = Path.Combine(root, "search");
        TestExtensionLocation.CopyTo(search);
        var dependent = InProcess("aaa.dependent", builder => builder.AddClassEmitter(new NamedClassEmitter("aaa.emitter")), dependsOn: "netprints.test");
        var independent = InProcess("bbb.independent", builder => builder.AddClassEmitter(new NamedClassEmitter("bbb.emitter")));

        await using ExtensionRegistry registry = Load(Options([dependent, independent], searchDirectories: [search]));

        Assert.Equal(["bbb.independent", "netprints.test", "aaa.dependent"], registry.Loaded.Select(m => m.Id));
        Assert.Equal(["bbb.emitter", "netprints.test/class", "aaa.emitter"], registry.ClassEmitters.Select(e => e.Id));
    }

    [Fact]
    public void ACancelledTokenThrowsOperationCanceled()
    {
        var loader = new ExtensionLoader(Options([BuiltInExtension.InProcessEntry]), new CollectingLoggerFactory());
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        Assert.Throws<OperationCanceledException>(() => loader.Load(cancelled.Token));
    }
}
