using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Core;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Extensibility;
using NetPrints.Extensibility.Loading;
using NetPrints.Reflection;

namespace NetPrints.Editor.Tests.Hosting;

public class ReflectionHostTests
{
    [Fact(Timeout = 120000)]
    public async Task ReloadPublishesTypesAndRaisesReloaded()
    {
        var host = new ReflectionHost(new InlineDispatcher(), TestExtensions.CreateBuiltIn(), NullLogger<ReflectionHost>.Instance);
        int reloaded = 0;
        host.Reloaded += (_, _) => reloaded++;

        Assert.Empty(host.NonStaticTypes);
        Assert.False(host.IsLoaded);
        Assert.False(host.Loaded.IsCompleted);
        Assert.Null(host.Snapshot);
        Assert.Throws<InvalidOperationException>(() => host.Provider); // no silent empty provider

        var project = Project.FromSnapshot(TestSnapshots.WithRuntimeAssemblies("P", "N"));
        await host.ReloadAsync(project, TestContext.Current.CancellationToken);

        Assert.Equal(1, reloaded);
        Assert.True(host.IsLoaded);
        Assert.True(host.Loaded.IsCompletedSuccessfully);
        Assert.Same(project.Snapshot, host.Snapshot);
        Assert.True(host.NonStaticTypes.Count > 4000);
        Assert.Empty(host.LastWarnings);
        Assert.True(host.Provider.GetNonStaticTypes().Contains(TypeSpecifier.FromType<string>()));
    }

    [Fact]
    public async Task ReloadRequiresASnapshot()
    {
        var host = new ReflectionHost(new InlineDispatcher(), TestExtensions.CreateBuiltIn(), NullLogger<ReflectionHost>.Instance);

        Project project = Project.FromSnapshot(TestSnapshots.Empty("P", "N"));
        project.Snapshot = null;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.ReloadAsync(project, TestContext.Current.CancellationToken));
    }

    [Fact(Timeout = 120000)]
    public async Task ProjectClassesAreVisibleToReflection()
    {
        var host = new ReflectionHost(new InlineDispatcher(), TestExtensions.CreateBuiltIn(), NullLogger<ReflectionHost>.Instance);
        var project = await TestPaths.LoadHelloWorldCopyAsync(TestContext.Current.CancellationToken);
        try
        {
            await host.ReloadAsync(project, TestContext.Current.CancellationToken);
            Assert.True(host.NonStaticTypes.Any(t => t.Name == "HelloWorld.Program"));
        }
        finally
        {
            TestPaths.TryDelete(project.Path);
        }
    }

    [Fact(Timeout = 120000)]
    public async Task ACatalogAddsItsTypesAndReplacesTheLiveTypesOfTheAssembliesItCovers()
    {
        var project = Project.FromSnapshot(TestSnapshots.WithRuntimeAssemblies("P", "N"));
        var plain = new ReflectionHost(new InlineDispatcher(), TestExtensions.CreateBuiltIn(), NullLogger<ReflectionHost>.Instance);
        await plain.ReloadAsync(project, TestContext.Current.CancellationToken);

        var manifest = new ExtensionManifest("editor.test", "Editor test", "1.0.0", string.Empty, "1.0", []);
        using var extensions = new ExtensionHost(
            new ExtensionLoaderOptions([], [], [BuiltInExtension.InProcessEntry, (manifest, new CatalogExtension())]), NullLoggerFactory.Instance);
        var withCatalog = new ReflectionHost(new InlineDispatcher(), extensions, NullLogger<ReflectionHost>.Instance);
        await withCatalog.ReloadAsync(project, TestContext.Current.CancellationToken);

        Assert.Contains(plain.NonStaticTypes, t => t.Name == "System.Text.RegularExpressions.Regex");
        Assert.DoesNotContain(withCatalog.NonStaticTypes, t => t.Name == "System.Text.RegularExpressions.Regex");
        Assert.Contains(withCatalog.NonStaticTypes, t => t.Name == "Acme.Widget");
        Assert.Contains(withCatalog.Provider.GetNonStaticTypes(), t => t.Name == "Acme.Widget");
        Assert.Contains(withCatalog.NonStaticTypes, t => t == TypeSpecifier.FromType<string>());
    }

    [Fact(Timeout = 120000)]
    public async Task TheTestExtensionsCatalogTypeReachesTheReflectionHost() // SC-004
    {
        var project = Project.FromSnapshot(TestSnapshots.WithRuntimeAssemblies("P", "N"));
        using ExtensionHost extensions = TestExtensionFolder.CreateHost();
        var host = new ReflectionHost(new InlineDispatcher(), extensions, NullLogger<ReflectionHost>.Instance);

        await host.ReloadAsync(project, TestContext.Current.CancellationToken);

        var widget = new TypeSpecifier("NetPrints.TestLib.Widget");
        Assert.Contains(host.NonStaticTypes, t => t == widget);
        Assert.Contains(host.Provider.GetNonStaticTypes(), t => t == widget);
        Assert.Contains(host.NonStaticTypes, t => t == TypeSpecifier.FromType<string>());
    }

    private sealed class CatalogExtension : INetPrintsExtension
    {
        public void Register(IExtensionBuilder builder) => builder.AddTypeCatalog(new InMemoryTypeCatalog(
            new CatalogInfo("editor.test/catalog", "1.0.0", ["System.Text.RegularExpressions"]),
            [new TypeSpecifier("Acme.Widget")], [], [], [],
            new Dictionary<TypeSpecifier, IReadOnlyList<string>>(), new Dictionary<MethodSpecifier, string>()));
    }
}
