using System;
using System.IO;
using System.Linq;
using System.Runtime.Loader;
using System.Threading.Tasks;
using NetPrints.Extensibility.Loading;
using Xunit;
using static NetPrints.Tests.Extensibility.ExtensionTestSupport;

namespace NetPrints.Tests.Extensibility.MultiExtension;

/// <summary>MX-T15: reloading the registry for a project reuses the load contexts of the folders it has loaded before.</summary>
[Collection(nameof(RealExtensionLoadCollection))]
public sealed class ReloadTests : IAsyncLifetime
{
    private readonly string root = Directory.CreateTempSubdirectory("netprints-reload-").FullName;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        Directory.Delete(root, recursive: true);
        return ValueTask.CompletedTask;
    }

    private static int FixtureContexts() =>
        AssemblyLoadContext.All.Count(context => context.Name is FixtureExtensions.Alpha or FixtureExtensions.Beta);

    [Fact]
    public async Task ThreeReloadsReuseTheContextsAndTheNumberOfLoadContextsStaysStable()
    {
        string[] folders = [FixtureExtensions.CopyTo(root, FixtureExtensions.Alpha), FixtureExtensions.CopyTo(root, FixtureExtensions.Beta)];
        await using var host = new ExtensionHost(ExtensionLoaderOptions.BuiltInOnly, new CollectingLoggerFactory());

        ExtensionRegistry first = await host.LoadForProjectAsync(folders, TestContext.Current.CancellationToken);
        Type firstNode = Assert.Single(first.NodeKinds, k => k.Kind == "fx.alpha/Ping").NodeType;
        int steady = FixtureContexts();

        for (int reload = 0; reload < 3; reload++)
        {
            ExtensionRegistry dropped = await host.LoadForProjectAsync([], TestContext.Current.CancellationToken);
            Assert.DoesNotContain(dropped.Loaded, m => m.Id == FixtureExtensions.Alpha);
            ExtensionRegistry again = await host.LoadForProjectAsync(folders, TestContext.Current.CancellationToken);

            Assert.NotSame(first, again);
            Assert.Same(firstNode, Assert.Single(again.NodeKinds, k => k.Kind == "fx.alpha/Ping").NodeType);
            Assert.Equal(steady, FixtureContexts());
        }
    }
}
