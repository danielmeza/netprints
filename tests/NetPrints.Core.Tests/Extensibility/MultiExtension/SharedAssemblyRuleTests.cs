using System;
using System.IO;
using System.Linq;
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
}
