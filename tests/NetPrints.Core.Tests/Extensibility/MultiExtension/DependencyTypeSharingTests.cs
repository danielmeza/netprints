using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NetPrints.Core;
using NetPrints.Extensibility.Loading;
using NetPrints.Testing.Extensions;
using Xunit;
using static NetPrints.Tests.Extensibility.ExtensionTestSupport;

namespace NetPrints.Tests.Extensibility.MultiExtension;

/// <summary>MX-T04 and MX-T05: an extension resolves assemblies from the extensions it depends on, in declared order (ADR-0010 §4).</summary>
[Collection(nameof(RealExtensionLoadCollection))]
public sealed class DependencyTypeSharingTests : IAsyncLifetime
{
    private const int DependencyAssemblyShadowedEvent = 2011;

    private readonly string root = Directory.CreateTempSubdirectory("netprints-dependency-sharing-").FullName;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        Directory.Delete(root, recursive: true);
        return ValueTask.CompletedTask;
    }

    private string Copy(string id) => FixtureExtensions.CopyTo(root, id);

    internal static Type SeenProviderType(ExtensionRegistry registry)
    {
        Type node = Assert.Single(registry.NodeKinds, k => k.Kind == "fx.types-consumer/Use").NodeType;
        Type extension = node.Assembly.GetType("Fx.TypesConsumer.TypesConsumerExtension") ?? throw new InvalidOperationException("Consumer extension type not found.");
        PropertyInfo property = extension.GetProperty("SeenProviderType") ?? throw new InvalidOperationException("SeenProviderType not found.");
        return Assert.IsAssignableFrom<Type>(property.GetValue(null));
    }

    [Fact]
    public async Task AConsumerSeesTheTypeOfItsProviderAndNotACopy()
    {
        await using ExtensionHarness harness = await ExtensionHarness.CreateAsync(
            [Copy(FixtureExtensions.TypesProvider), Copy(FixtureExtensions.TypesConsumer)], [], TestContext.Current.CancellationToken);

        Assert.Empty(harness.Registry.Results.OfType<ExtensionLoadResult.Failed>());
        Type seen = SeenProviderType(harness.Registry);
        AssemblyLoadContext providerContext = Assert.IsAssignableFrom<AssemblyLoadContext>(AssemblyLoadContext.GetLoadContext(seen.Assembly));
        Assert.Equal(FixtureExtensions.TypesProvider, providerContext.Name);
        var consumerNode = Assert.Single(harness.Registry.NodeKinds, k => k.Kind == "fx.types-consumer/Use").NodeType;
        AssemblyLoadContext consumerContext = Assert.IsAssignableFrom<AssemblyLoadContext>(AssemblyLoadContext.GetLoadContext(consumerNode.Assembly));
        Assert.Equal(FixtureExtensions.TypesConsumer, consumerContext.Name);
        Assert.DoesNotContain(consumerContext.Assemblies, a => a.GetName().Name == "Fx.TypesProvider");
        Assert.Single(providerContext.Assemblies, a => a.GetName().Name == "Fx.TypesProvider");
    }

    [Fact]
    public async Task AConsumersEmitterOutputUsesTheProvidersType()
    {
        await using ExtensionHarness harness = await ExtensionHarness.CreateAsync(
            [Copy(FixtureExtensions.TypesProvider), Copy(FixtureExtensions.TypesConsumer)], [], TestContext.Current.CancellationToken);

        string code = harness.Translate(MultiExtensionGraphs.BuildClass(harness.Registry, "Ns", "Consumed"));

        Assert.Contains("Description(\"fx.types-provider\")", code, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AProviderThatFailsToLoadFailsItsConsumerWithNpx003()
    {
        string provider = Copy(FixtureExtensions.TypesProvider);
        File.Delete(Path.Combine(provider, "Fx.TypesProvider.dll"));

        await using ExtensionHarness harness = await ExtensionHarness.CreateAsync(
            [provider, Copy(FixtureExtensions.TypesConsumer)], [], TestContext.Current.CancellationToken);

        Assert.Equal("NPX007", SingleFailure(harness.Registry, FixtureExtensions.TypesProvider).Code);
        Assert.Equal("NPX003", SingleFailure(harness.Registry, FixtureExtensions.TypesConsumer).Code);
    }

    [Fact]
    public async Task ACopyOfTheProvidersAssemblyInTheConsumerFolderIsIgnoredAndLogged()
    {
        string provider = Copy(FixtureExtensions.TypesProvider);
        string consumer = Copy(FixtureExtensions.TypesConsumer);
        File.Copy(Path.Combine(provider, "Fx.TypesProvider.dll"), Path.Combine(consumer, "Fx.TypesProvider.dll"));
        var logs = new CollectingLoggerFactory();

        await using ExtensionRegistry registry = Load(Options(folders: [provider, consumer]), logs);

        Assert.Empty(registry.Results.OfType<ExtensionLoadResult.Failed>());
        Type seen = SeenProviderType(registry);
        Assert.Equal(FixtureExtensions.TypesProvider, AssemblyLoadContext.GetLoadContext(seen.Assembly)?.Name);
        var entry = Assert.Single(logs.Entries, e => e.EventId.Id == DependencyAssemblyShadowedEvent);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("Fx.TypesProvider", entry.Message, StringComparison.Ordinal);
        Assert.Contains(FixtureExtensions.TypesProvider, entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ADiamondTakesTheLibraryFromTheFirstDependencyInDeclaredOrderWhateverTheDiscoveryOrder()
    {
        string[] ids = [FixtureExtensions.LibV1, FixtureExtensions.LibV2, FixtureExtensions.Diamond];

        foreach (string[] declared in new[] { new[] { FixtureExtensions.LibV1, FixtureExtensions.LibV2 }, [FixtureExtensions.LibV2, FixtureExtensions.LibV1] })
        {
            foreach (string[] order in Permutations(ids))
            {
                string scenario = $"declared [{string.Join(", ", declared)}], discovered [{string.Join(", ", order)}]";
                string rootFor = Path.Combine(root, string.Join("-", declared.Concat(order)).Replace("fx.", string.Empty, StringComparison.Ordinal));
                string[] folders = [.. order.Select(id => FixtureExtensions.CopyTo(rootFor, id))];
                string diamond = folders[Array.IndexOf(order, FixtureExtensions.Diamond)];
                string manifest = Path.Combine(diamond, "netprints-extension.json");
                File.WriteAllText(manifest, File.ReadAllText(manifest).Replace(
                    "[\"fx.libv1\", \"fx.libv2\"]", $"[{string.Join(", ", declared.Select(id => $"\"{id}\""))}]", StringComparison.Ordinal));

                await using ExtensionHarness harness = await ExtensionHarness.CreateAsync(folders, [], TestContext.Current.CancellationToken);

                Assert.True(!harness.Registry.Results.OfType<ExtensionLoadResult.Failed>().Any(), scenario);
                var node = Assert.Single(harness.Registry.NodeKinds, k => k.Kind == "fx.diamond/Describe").NodeType;
                Type extension = node.Assembly.GetType("Fx.Diamond.DiamondExtension") ?? throw new InvalidOperationException("Diamond extension type not found.");
                Assembly seen = Assert.IsAssignableFrom<Assembly>(extension.GetProperty("SeenSharedLib")?.GetValue(null));
                Assert.True(declared[0] == AssemblyLoadContext.GetLoadContext(seen)?.Name, scenario);
                Assert.Equal(declared[0] == FixtureExtensions.LibV1 ? new Version(1, 0, 0, 0) : new Version(2, 0, 0, 0), seen.GetName().Version);
                string code = harness.Translate(MultiExtensionGraphs.BuildClass(harness.Registry, "Ns", "Diamonds", "fx.diamond/Describe"));
                Assert.Contains(declared[0] == FixtureExtensions.LibV1 ? "shared-lib-v1" : "shared-lib-v2", code, StringComparison.Ordinal);
                Assert.Equal(FixtureExtensions.Diamond, AssemblyLoadContext.GetLoadContext(node.Assembly)?.Name);
            }
        }
    }

    private static string[][] Permutations(string[] items) =>
        items.Length == 1
            ? [items]
            : [.. items.SelectMany((item, index) => Permutations([.. items.Where((_, other) => other != index)]).Select(rest => (string[])[item, .. rest]))];
}
