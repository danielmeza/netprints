using System.Linq;
using NetPrints.Core;
using NetPrints.Reflection;
using Xunit;

namespace NetPrints.Catalog.Tests.Runtime;

/// <summary>AB-10: the node hints of a catalog reach node search through <see cref="INodeHintSource"/>.</summary>
public sealed class NodeHintTests
{
    private static readonly ITypeCatalog Catalog = CatalogLoader.Load(FixtureCatalog.BuildFixture(BuiltInCatalogProfiles.Annotated, FixtureCatalog.FixtureId).Document);

    private static readonly TypeSpecifier Counter = new("Fixture.Utilities.Counter");

    private static readonly TypeSpecifier Helpers = new("Fixture.Utilities.Helpers");

    private static MethodSpecifier MethodOf(IReflectionProvider provider, TypeSpecifier type, string name) =>
        provider.GetMethods(new ReflectionProviderMethodQuery().WithType(type)).First(method => method.Name == name && method.DeclaringType == type);

    [Fact]
    public void ATypeHintCarriesTheDisplayNameAndCategory()
    {
        NodeHint hint = Assert.IsAssignableFrom<INodeHintSource>(Catalog).GetTypeHint(Counter) ?? throw new Xunit.Sdk.XunitException("no hint");

        Assert.Equal("Counter Widget", hint.DisplayName);
        Assert.Equal("Widgets", hint.Category);
    }

    [Fact]
    public void AMethodHintCarriesTheKeywords()
    {
        var source = Assert.IsAssignableFrom<INodeHintSource>(Catalog);

        Assert.Equal(["increment"], source.GetMethodHint(MethodOf(Catalog, Counter, "Step"))?.Keywords);
        Assert.Equal(["multiply", "twice"], source.GetMethodHint(MethodOf(Catalog, Helpers, "Double"))?.Keywords);
        Assert.Equal("Double", source.GetMethodHint(MethodOf(Catalog, Helpers, "Double"))?.DisplayName);
    }

    [Fact]
    public void AMemberWithNoHintAnswersNull()
    {
        var source = Assert.IsAssignableFrom<INodeHintSource>(Catalog);

        Assert.Null(source.GetTypeHint(new TypeSpecifier("Fixture.Unknown.Nothing")));
    }

    [Fact]
    public void TheCompositeAndMemoizedProvidersForwardTheHints()
    {
        MethodSpecifier step = MethodOf(Catalog, Counter, "Step");
        var composite = new CompositeReflectionProvider([Catalog]);
        var memoized = new MemoizedReflectionProvider(composite);

        Assert.Equal(["increment"], Assert.IsAssignableFrom<INodeHintSource>(composite).GetMethodHint(step)?.Keywords);
        Assert.Equal(["increment"], Assert.IsAssignableFrom<INodeHintSource>(memoized).GetMethodHint(step)?.Keywords);
        Assert.Equal("Counter Widget", Assert.IsAssignableFrom<INodeHintSource>(memoized).GetTypeHint(Counter)?.DisplayName);
    }
}
