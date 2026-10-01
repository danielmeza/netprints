using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Core;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Extensibility.Loading;
using NetPrints.Reflection;
using NetPrints.Testing;

namespace NetPrints.Editor.Tests.Reflection;

/// <summary>CT-T15 (editor half): the <c>fx.catalog</c> extension's catalog reaches the editor's reflection host, so node search offers <c>Vector2.Add</c>.</summary>
public sealed class CatalogSearchTests
{
    private static readonly TypeSpecifier Vector2 = new("Fixture.Geometry.Vector2");

    [Fact(Timeout = 120000)]
    public async Task TheFixtureCatalogExtensionMakesVector2AddSearchable()
    {
        var project = Project.FromSnapshot(TestSnapshots.WithRuntimeAssemblies("P", "N"));
        await using var extensions = new ExtensionHost(ExtensionLoaderOptions.BuiltInOnly with { ExtensionFolders = [FixtureExtensions.CatalogFolder()] }, NullLoggerFactory.Instance);
        var host = new ReflectionHost(new InlineDispatcher(), extensions, NullLogger<ReflectionHost>.Instance);

        await host.ReloadAsync(project, TestContext.Current.CancellationToken);

        Assert.Contains(host.NonStaticTypes, t => t == Vector2);
        MethodSpecifier add = Assert.Single(
            host.Provider.GetMethods(new ReflectionProviderMethodQuery().WithType(Vector2).WithStatic(false)),
            m => m.Name == "Add");
        Assert.Equal(Vector2, add.DeclaringType);
    }

    [Fact(Timeout = 120000)]
    public async Task WithoutTheExtensionVector2IsNotOffered()
    {
        var project = Project.FromSnapshot(TestSnapshots.WithRuntimeAssemblies("P", "N"));
        var host = new ReflectionHost(new InlineDispatcher(), TestExtensions.CreateBuiltIn(), NullLogger<ReflectionHost>.Instance);

        await host.ReloadAsync(project, TestContext.Current.CancellationToken);

        Assert.DoesNotContain(host.NonStaticTypes, t => t == Vector2);
    }
}
