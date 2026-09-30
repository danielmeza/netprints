using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;
using NetPrints.Core;
using NetPrints.Extensibility.Loading;
using NetPrints.Testing.Extensions;
using Xunit;

namespace NetPrints.Tests.Extensibility.MultiExtension;

/// <summary>MX-T06: two extensions that privately ship different versions of one library load together and never see each other's copy.</summary>
[Collection(nameof(RealExtensionLoadCollection))]
public sealed class VersionIsolationTests : IAsyncLifetime
{
    private readonly string root = Directory.CreateTempSubdirectory("netprints-version-isolation-").FullName;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        Directory.Delete(root, recursive: true);
        return ValueTask.CompletedTask;
    }

    private static Assembly SeenSharedLib(ExtensionRegistry registry, string kind, string extensionType)
    {
        Type node = Assert.Single(registry.NodeKinds, k => k.Kind == kind).NodeType;
        Type extension = node.Assembly.GetType(extensionType) ?? throw new InvalidOperationException($"{extensionType} not found.");
        PropertyInfo property = extension.GetProperty("SeenSharedLib") ?? throw new InvalidOperationException("SeenSharedLib not found.");
        return Assert.IsAssignableFrom<Assembly>(property.GetValue(null));
    }

    [Fact]
    public async Task TwoVersionsOfOneLibraryLoadTogetherEachTranslatorCallsItsOwnAndTheAssembliesAreDistinct()
    {
        string v1 = FixtureExtensions.CopyTo(root, FixtureExtensions.LibV1);
        string v2 = FixtureExtensions.CopyTo(root, FixtureExtensions.LibV2);

        await using ExtensionHarness harness = await ExtensionHarness.CreateAsync([v1, v2], [], TestContext.Current.CancellationToken);

        Assert.Empty(harness.Registry.Results.OfType<ExtensionLoadResult.Failed>());
        string code = harness.Translate(MultiExtensionGraphs.BuildClass(harness.Registry, "Ns", "Libs", "fx.libv1/Describe", "fx.libv2/Describe"));
        Assert.Contains("System.Console.WriteLine(\"shared-lib-v1\");", code, StringComparison.Ordinal);
        Assert.Contains("System.Console.WriteLine(\"shared-lib-v2:fx.libv2\");", code, StringComparison.Ordinal);

        Assembly first = SeenSharedLib(harness.Registry, "fx.libv1/Describe", "Fx.LibV1.LibV1Extension");
        Assembly second = SeenSharedLib(harness.Registry, "fx.libv2/Describe", "Fx.LibV2.LibV2Extension");
        Assert.NotSame(first, second);
        Assert.Equal("Fixture.SharedLib", first.GetName().Name);
        Assert.Equal("Fixture.SharedLib", second.GetName().Name);
        Assert.Equal(FixtureExtensions.LibV1, AssemblyLoadContext.GetLoadContext(first)?.Name);
        Assert.Equal(FixtureExtensions.LibV2, AssemblyLoadContext.GetLoadContext(second)?.Name);
    }
}
