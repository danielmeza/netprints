using System;
using System.IO;
using System.Linq;
using System.Runtime.Loader;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NetPrints.Core;
using NetPrints.Extensibility.Loading;
using NetPrints.Graph;
using NetPrints.Testing.Extensions;
using Xunit;
using static NetPrints.Tests.Extensibility.ExtensionTestSupport;

namespace NetPrints.Tests.Extensibility.MultiExtension;

/// <summary>MX-T02 and MX-T03: an extension shares what the host provides, not what a name prefix suggests (ADR-0010 §4).</summary>
[Collection(nameof(RealExtensionLoadCollection))]
public sealed class SharedAssemblyRuleTests : IAsyncLifetime
{
    private const int HostAssemblyShadowedEvent = 2010;

    private readonly string root = Directory.CreateTempSubdirectory("netprints-shared-rule-").FullName;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        Directory.Delete(root, recursive: true);
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task APrivateDependencyWhoseNameStartsWithNetPrintsLoadsFromTheExtensionFolder()
    {
        string folder = FixtureExtensions.CopyTo(root, FixtureExtensions.PrefixedPrivate);

        await using ExtensionHarness harness = await ExtensionHarness.CreateAsync([folder], [], TestContext.Current.CancellationToken);

        Assert.Contains(FixtureExtensions.PrefixedPrivate, harness.Registry.Loaded.Select(m => m.Id));
        ClassGraph cls = MultiExtensionGraphs.BuildClass(harness.Registry, "Ns", "Prefixed", "fx.private-prefix/Describe");
        Assert.Contains("System.Console.WriteLine(\"netprints-fixture-runtime\");", harness.Translate(cls), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ACopyOfAHostAssemblyInTheExtensionFolderIsIgnoredAndLogged()
    {
        string folder = FixtureExtensions.CopyTo(root, FixtureExtensions.Alpha);
        File.Copy(typeof(Node).Assembly.Location, Path.Combine(folder, "NetPrints.Core.dll"));
        var logs = new CollectingLoggerFactory();

        await using ExtensionRegistry registry = Load(Options(folders: [folder]), logs);

        Assert.Equal([FixtureExtensions.Alpha], registry.Loaded.Select(m => m.Id));
        var ping = Assert.Single(registry.NodeKinds);
        Assert.Same(typeof(Node), ping.NodeType.BaseType);
        var entry = Assert.Single(logs.Entries, e => e.EventId.Id == HostAssemblyShadowedEvent);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("fx.alpha", entry.Message, StringComparison.Ordinal);
        Assert.Contains("NetPrints.Core", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AFolderWithoutHostAssemblyCopiesLogsNoShadowWarning()
    {
        string folder = FixtureExtensions.CopyTo(root, FixtureExtensions.PrefixedPrivate);
        var logs = new CollectingLoggerFactory();

        await using ExtensionRegistry registry = Load(Options(folders: [folder]), logs);

        Assert.DoesNotContain(logs.Entries, e => e.EventId.Id == HostAssemblyShadowedEvent);
    }

    [Fact]
    public async Task AStrayDllWithAnUnparsableNameDoesNotFailTheExtensionLoad()
    {
        string folder = FixtureExtensions.CopyTo(root, FixtureExtensions.Alpha);
        File.WriteAllBytes(Path.Combine(folder, "odd,name.dll"), [0]);

        await using ExtensionRegistry registry = Load(Options(folders: [folder]));

        Assert.Equal([FixtureExtensions.Alpha], registry.Loaded.Select(m => m.Id));
    }

    [Fact]
    public async Task AShadowedHostAssemblyIsLoggedOncePerContextAcrossReloads()
    {
        string folder = FixtureExtensions.CopyTo(root, FixtureExtensions.Alpha);
        File.Copy(typeof(Node).Assembly.Location, Path.Combine(folder, "NetPrints.Core.dll"));
        var logs = new CollectingLoggerFactory();
        await using var host = new ExtensionHost(ExtensionLoaderOptions.BuiltInOnly, logs);

        await host.LoadForProjectAsync([folder], TestContext.Current.CancellationToken);
        await host.LoadForProjectAsync([], TestContext.Current.CancellationToken);
        await host.LoadForProjectAsync([folder], TestContext.Current.CancellationToken);

        Assert.Single(logs.Entries, e => e.EventId.Id == HostAssemblyShadowedEvent);
    }

    [Fact]
    public async Task AnAssemblyTheHostLoadsAfterTheContextWasCreatedIsStillPrivateToTheExtension()
    {
        string hostCopy = Path.Combine(root, "host");
        Compile(hostCopy, "Gf1Late", "namespace Gf1Late { public static class Marker { } }");
        string folder = Path.Combine(root, "gf1.late");
        Compile(folder, "Gf1Late", "namespace Gf1Late { public static class Marker { } }");
        Compile(folder, "Gf1LateExt", """
            using System.Reflection;
            using NetPrints.Extensibility;
            namespace Gf1LateExt;
            public sealed class Ext : INetPrintsExtension
            {
                public void Register(IExtensionBuilder builder) { }
                public static string Touch() => Assembly.Load("Gf1Late").Location;
            }
            """);
        WriteManifest(folder, ManifestJson("gf1.late", "Gf1LateExt.dll"));
        await using ExtensionRegistry registry = Load(Options(folders: [folder]));
        Assert.Equal(["gf1.late"], registry.Loaded.Select(m => m.Id));

        AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(hostCopy, "Gf1Late.dll"));

        AssemblyLoadContext context = Assert.Single(AssemblyLoadContext.All, c => c.Name == "gf1.late");
        Type ext = Assert.Single(context.Assemblies, a => a.GetName().Name == "Gf1LateExt").GetType("Gf1LateExt.Ext") ?? throw new InvalidOperationException("Ext not found.");
        string location = Assert.IsType<string>(ext.GetMethod("Touch")?.Invoke(null, null));
        Assert.Equal(Path.Combine(folder, "Gf1Late.dll"), location);
    }
}
