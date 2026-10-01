using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NetPrints.Extensibility.Loading;
using NetPrints.Testing.Extensions;
using Xunit;

namespace NetPrints.Tests.Extensibility.MultiExtension;

/// <summary>MX-T09: the order extension folders are discovered in changes neither what is loaded, nor the registry order, nor the generated code.</summary>
[Collection(nameof(RealExtensionLoadCollection))]
public sealed class LoadOrderPermutationTests : IAsyncLifetime
{
    private static readonly string[] Ids =
        [FixtureExtensions.Alpha, FixtureExtensions.Beta, FixtureExtensions.LibV1, FixtureExtensions.PrefixedPrivate];

    private static readonly string[] Kinds = ["fx.alpha/Ping", "fx.beta/Pong", "fx.libv1/Describe", "fx.private-prefix/Describe"];

    private readonly string root = Directory.CreateTempSubdirectory("netprints-load-order-").FullName;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        Directory.Delete(root, recursive: true);
        return ValueTask.CompletedTask;
    }

    private static IEnumerable<T[]> Permutations<T>(IReadOnlyList<T> items)
    {
        if (items.Count <= 1)
        {
            yield return [.. items];
            yield break;
        }

        for (int index = 0; index < items.Count; index++)
        {
            T[] rest = [.. items.Where((_, i) => i != index)];
            foreach (T[] tail in Permutations(rest))
            {
                yield return [items[index], .. tail];
            }
        }
    }

    [Fact]
    public async Task AllTwentyFourDiscoveryOrdersLoadTheSameExtensionsInTheSameOrderAndGenerateTheSameCode()
    {
        Dictionary<string, string> folders = Ids.ToDictionary(id => id, id => FixtureExtensions.CopyTo(root, id), StringComparer.Ordinal);
        string[]? loadedOrder = null;
        string[]? registryOrder = null;
        string? code = null;
        int orders = 0;

        foreach (string[] permutation in Permutations(Ids))
        {
            orders++;
            await using ExtensionHarness harness = await ExtensionHarness.CreateAsync([.. permutation.Select(id => folders[id])], [], TestContext.Current.CancellationToken);

            Assert.Empty(harness.Registry.Results.OfType<ExtensionLoadResult.Failed>());
            string[] loaded = [.. harness.Registry.Loaded.Select(m => m.Id)];
            string[] kinds = [.. harness.Registry.NodeKinds.Select(k => k.Kind).Where(k => k.StartsWith("fx.", StringComparison.Ordinal))];
            string generated = harness.Translate(MultiExtensionGraphs.BuildClass(harness.Registry, "Ns", "AllFour", Kinds));

            loadedOrder ??= loaded;
            registryOrder ??= kinds;
            code ??= generated;
            Assert.Equal(loadedOrder, loaded);
            Assert.Equal(registryOrder, kinds);
            Assert.Equal(code, generated);
        }

        Assert.Equal(24, orders);
        Assert.NotNull(loadedOrder);
        Assert.NotNull(registryOrder);
        Assert.Equal(["netprints", FixtureExtensions.Alpha, FixtureExtensions.Beta, FixtureExtensions.LibV1, FixtureExtensions.PrefixedPrivate], loadedOrder);
        Assert.Equal(["fx.alpha/Ping", "fx.beta/Pong", "fx.libv1/Describe", "fx.private-prefix/Describe"], registryOrder);
        Assert.NotNull(code);
        Assert.Contains("Description(\"fx.alpha\")", code, StringComparison.Ordinal);
        Assert.Contains("Description(\"fx.beta\")", code, StringComparison.Ordinal);
        Assert.Contains("shared-lib-v1", code, StringComparison.Ordinal);
        Assert.Contains("netprints-fixture-runtime", code, StringComparison.Ordinal);
    }
}
