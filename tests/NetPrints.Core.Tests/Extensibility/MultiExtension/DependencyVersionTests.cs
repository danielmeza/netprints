using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NetPrints.Extensibility.Loading;
using NetPrints.Testing.Extensions;
using Xunit;
using static NetPrints.Tests.Extensibility.ExtensionTestSupport;

namespace NetPrints.Tests.Extensibility.MultiExtension;

/// <summary>G-R3: a dependency's copy of an assembly may not be older than the version the consumer was built against (ADR-0010 §4).</summary>
[Collection(nameof(RealExtensionLoadCollection))]
public sealed class DependencyVersionTests : IAsyncLifetime
{
    private readonly string root = Directory.CreateTempSubdirectory("netprints-dependency-version-").FullName;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        Directory.Delete(root, recursive: true);
        return ValueTask.CompletedTask;
    }

    private string Copy(string id, string? dependsOn = null, bool dropOwnLibrary = false)
    {
        string folder = FixtureExtensions.CopyTo(root, id);
        if (dependsOn is not null)
        {
            string manifest = Path.Combine(folder, "netprints-extension.json");
            string text = File.ReadAllText(manifest);
            int start = text.IndexOf("\"dependsOn\"", StringComparison.Ordinal);
            File.WriteAllText(manifest, string.Concat(text.AsSpan(0, start), "\"dependsOn\": [", dependsOn, "]\n}\n"));
        }

        if (dropOwnLibrary)
        {
            File.Delete(Path.Combine(folder, "Fixture.SharedLib.dll"));
        }

        return folder;
    }

    [Fact]
    public async Task AConsumerBuiltAgainstVersion2WithAProviderShippingVersion1FailsWithNpx008AndIsNotRegistered()
    {
        string provider = Copy(FixtureExtensions.LibV1);
        string consumer = Copy(FixtureExtensions.LibV2, "\"fx.libv1\"", dropOwnLibrary: true);

        await using ExtensionHarness harness = await ExtensionHarness.CreateAsync([provider, consumer], [], TestContext.Current.CancellationToken);

        ExtensionLoadResult.Failed failure = SingleFailure(harness.Registry, FixtureExtensions.LibV2);
        Assert.Equal("NPX008", failure.Code);
        Assert.Contains("Fixture.SharedLib", failure.Reason, StringComparison.Ordinal);
        Assert.Contains("1.0.0.0", failure.Reason, StringComparison.Ordinal);
        Assert.Contains("2.0.0.0", failure.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain(harness.Registry.NodeKinds, k => k.Kind == "fx.libv2/Describe");
        Assert.Contains(harness.Registry.NodeKinds, k => k.Kind == "fx.libv1/Describe");
    }

    [Fact]
    public async Task AConsumerBuiltAgainstVersion1WithAProviderShippingVersion1Loads()
    {
        string provider = Copy(FixtureExtensions.LibV1);
        string consumer = Copy(FixtureExtensions.Diamond, "\"fx.libv1\"");

        await using ExtensionHarness harness = await ExtensionHarness.CreateAsync([provider, consumer], [], TestContext.Current.CancellationToken);

        Assert.Empty(harness.Registry.Results.OfType<ExtensionLoadResult.Failed>());
        Assert.Contains("shared-lib-v1", harness.Translate(MultiExtensionGraphs.BuildClass(harness.Registry, "Ns", "Equal", "fx.diamond/Describe")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AConsumerBuiltAgainstVersion1WithAProviderShippingVersion2Loads()
    {
        string provider = Copy(FixtureExtensions.LibV2);
        string consumer = Copy(FixtureExtensions.Diamond, "\"fx.libv2\"");

        await using ExtensionHarness harness = await ExtensionHarness.CreateAsync([provider, consumer], [], TestContext.Current.CancellationToken);

        Assert.Empty(harness.Registry.Results.OfType<ExtensionLoadResult.Failed>());
        Assert.Contains("shared-lib-v2", harness.Translate(MultiExtensionGraphs.BuildClass(harness.Registry, "Ns", "Higher", "fx.diamond/Describe")), StringComparison.Ordinal);
    }
}
