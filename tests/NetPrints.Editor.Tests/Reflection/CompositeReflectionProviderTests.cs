using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Compilation;
using NetPrints.Core;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Projects;
using NetPrints.Reflection;
using NetPrints.Testing;
using Project = NetPrints.Core.Project;

namespace NetPrints.Editor.Tests.Reflection;

/// <summary>EX-T06: catalogs and the composite provider (extension-points.md §4).</summary>
public class CompositeReflectionProviderTests(RuntimeReflectionFixture fixture) : IClassFixture<RuntimeReflectionFixture>
{
    private static readonly TypeSpecifier Widget = new("Acme.Widget", false, false, []);
    private static readonly TypeSpecifier Gadget = new("Acme.Gadget", false, false, []);

    private static InMemoryTypeCatalog Catalog(string id, IReadOnlyList<TypeSpecifier> types,
        IReadOnlyList<MethodSpecifier>? methods = null,
        IReadOnlyDictionary<MethodSpecifier, string>? documentation = null,
        IReadOnlyList<string>? coveredAssemblies = null) =>
        new(new CatalogInfo(id, "1.0", coveredAssemblies ?? ["Acme"]),
            types,
            methods ?? [],
            [],
            [],
            new Dictionary<TypeSpecifier, IReadOnlyList<string>>(),
            documentation ?? new Dictionary<MethodSpecifier, string>());

    private static MethodSpecifier Method(string name, TypeSpecifier declaringType) =>
        new(name, [], [], MethodModifiers.Static, MemberVisibility.Public, declaringType, []);

    [Fact]
    public void EmptyProviderListIsRejected()
    {
        Assert.Throws<ArgumentException>(() => new CompositeReflectionProvider([]));
    }

    [Fact]
    public void CatalogTypesComeFirstAndAreDistinct()
    {
        var first = Catalog("first", [Widget, Gadget]);
        var second = Catalog("second", [Gadget, TypeSpecifier.FromType<string>()]);
        var composite = new CompositeReflectionProvider([first, second]);

        Assert.Equal([Widget, Gadget, TypeSpecifier.FromType<string>()], composite.GetNonStaticTypes());
    }

    [Fact]
    public void ProvidersPropertyKeepsTheGivenOrder()
    {
        var first = Catalog("first", []);
        var second = Catalog("second", []);

        Assert.Equal<IReflectionProvider>([first, second], new CompositeReflectionProvider([first, second]).Providers);
    }

    [Fact]
    public void CoveredAssemblyNamesAreExcludedFromLiveEnumeration()
    {
        ITypeCatalog catalog = Catalog("core", [TypeSpecifier.FromType<string>()], coveredAssemblies: ["System.Private.CoreLib"]);
        var live = new ReflectionProvider(fixture.Assemblies, [], new HashSet<string>(catalog.Info.CoveredAssemblyNames));
        var composite = new CompositeReflectionProvider([catalog, live]);

        Assert.DoesNotContain(TypeSpecifier.FromType<string>(), live.GetNonStaticTypes());
        Assert.Contains(TypeSpecifier.FromType<Uri>(), live.GetNonStaticTypes());

        var types = composite.GetNonStaticTypes().ToList();
        Assert.Equal(TypeSpecifier.FromType<string>(), types[0]);
        Assert.Equal(1, types.Count(type => type == TypeSpecifier.FromType<string>()));
    }

    [Fact]
    public void ImplicitCastAndSubclassAreTrueWhenAnyProviderSaysSo()
    {
        var live = new ReflectionProvider(fixture.Assemblies, [], new HashSet<string>());
        var composite = new CompositeReflectionProvider([Catalog("acme", [Widget]), live]);
        TypeSpecifier intType = TypeSpecifier.FromType<int>();
        TypeSpecifier longType = TypeSpecifier.FromType<long>();
        TypeSpecifier stringType = TypeSpecifier.FromType<string>();
        TypeSpecifier objectType = TypeSpecifier.FromType<object>();

        Assert.True(composite.HasImplicitCast(intType, longType));
        Assert.False(composite.HasImplicitCast(longType, intType));
        Assert.True(composite.TypeSpecifierIsSubclassOf(stringType, objectType));
        Assert.False(composite.TypeSpecifierIsSubclassOf(objectType, stringType));
    }

    [Fact]
    public void MethodsAreConcatenatedInProviderOrderWithoutDuplicates()
    {
        MethodSpecifier spawn = Method("Spawn", Widget);
        MethodSpecifier destroy = Method("Destroy", Widget);
        var first = Catalog("first", [Widget], [spawn]);
        var second = Catalog("second", [Widget], [Method("Spawn", Widget), destroy]);
        var composite = new CompositeReflectionProvider([first, second]);

        var methods = composite.GetMethods(new ReflectionProviderMethodQuery().WithType(Widget)).ToList();

        Assert.Equal(["Spawn", "Destroy"], methods.Select(method => method.Name));
        Assert.Same(spawn, methods[0]);
    }

    [Fact]
    public void DocumentationComesFromTheFirstProviderThatHasIt()
    {
        MethodSpecifier spawn = Method("Spawn", Widget);
        var silent = Catalog("silent", [Widget], [spawn]);
        var first = Catalog("first", [Widget], [spawn], new Dictionary<MethodSpecifier, string> { [spawn] = "from first" });
        var second = Catalog("second", [Widget], [spawn], new Dictionary<MethodSpecifier, string> { [spawn] = "from second" });

        Assert.Equal("from first", new CompositeReflectionProvider([silent, first, second]).GetMethodDocumentation(spawn));
        Assert.Null(new CompositeReflectionProvider([silent]).GetMethodDocumentation(spawn));
    }

    private static readonly TypeSpecifier Greeter = new("AnnotatedFixture.Greeter");
    private static readonly TypeSpecifier Unlisted = new("AnnotatedFixture.Unlisted");
    private static readonly TypeSpecifier Boxed = new("Mine.Boxed");
    private static readonly string CoreLib = typeof(object).Assembly.GetName().Name ?? string.Empty;

    private static async Task<ReflectionHost> HostReferencingAnnotatedLibAsync()
    {
        ProjectSnapshot runtime = TestSnapshots.WithRuntimeAssemblies("P", "N");
        var project = Project.FromSnapshot(runtime with
        {
            References = [.. runtime.References, new ResolvedAssembly(FixtureExtensions.AnnotatedLibraryAssembly(), null)],
        });
        var host = new ReflectionHost(new InlineDispatcher(), TestExtensions.CreateBuiltIn(), NullLogger<ReflectionHost>.Instance);
        await host.ReloadAsync(project, TestContext.Current.CancellationToken);
        return host;
    }

    [Fact(Timeout = 120000)]
    public async Task AScopedQueryForACatalogedTypeReturnsOnlyTheCatalogsMembers()
    {
        ReflectionHost host = await HostReferencingAnnotatedLibAsync();

        var declared = host.Provider.GetMethods(new ReflectionProviderMethodQuery().WithType(Greeter)).Where(method => method.DeclaringType == Greeter).Select(method => method.Name).ToList();

        Assert.Equal(["Count", "Greet"], declared.Order(StringComparer.Ordinal));
        Assert.DoesNotContain("Undeclared", declared);
    }

    [Fact(Timeout = 120000)]
    public async Task AScopedQueryForATypeTheCoveringCatalogDoesNotListReturnsNothingAndNamesTheCatalog()
    {
        ReflectionHost host = await HostReferencingAnnotatedLibAsync();

        Assert.Empty(host.Provider.GetMethods(new ReflectionProviderMethodQuery().WithType(Unlisted)));
        Assert.Empty(host.Provider.GetVariables(new ReflectionProviderVariableQuery().WithType(Unlisted)));
        var scope = Assert.IsAssignableFrom<ICatalogScope>(host.Provider);
        Assert.Equal("catalogannotatedlib", scope.GetHidingCatalogId(Unlisted));
        Assert.Null(scope.GetHidingCatalogId(Greeter));
        Assert.Null(scope.GetHidingCatalogId(TypeSpecifier.FromType<Uri>()));
    }

    [Fact(Timeout = 120000)]
    public async Task AScopedQueryForATypeNoCatalogCoversAnswersAsTheLiveProviderDid()
    {
        ReflectionHost host = await HostReferencingAnnotatedLibAsync();
        var before = new ReflectionProvider(fixture.Assemblies, [], new HashSet<string>());
        var uri = new ReflectionProviderMethodQuery().WithType(TypeSpecifier.FromType<Uri>());

        Assert.Equal(before.GetMethods(uri), host.Provider.GetMethods(uri));
        Assert.Equal(
            before.GetVariables(new ReflectionProviderVariableQuery().WithType(TypeSpecifier.FromType<Uri>())).Select(variable => variable.Name).Distinct(),
            host.Provider.GetVariables(new ReflectionProviderVariableQuery().WithType(TypeSpecifier.FromType<Uri>())).Select(variable => variable.Name).Distinct());
    }

    [Fact(Timeout = 120000)]
    public async Task HiddenMembersStillResolveForExistingNodes()
    {
        ReflectionHost host = await HostReferencingAnnotatedLibAsync();
        var undeclared = new MethodSpecifier("Undeclared", [], [TypeSpecifier.FromType<string>()], MethodModifiers.None, MemberVisibility.Public, Greeter, []);

        Assert.Contains(host.Provider.GetPublicMethodOverloads(undeclared), method => method.Name == "Undeclared");
        Assert.Contains(host.Provider.GetNonStaticTypes(), type => type == Greeter);
    }

    [Fact]
    public void InheritedMembersComeFromTheSourceCoveringTheBaseTypesAssemblyAndProjectTypesAreLive()
    {
        const string Source = "namespace Mine { public class Boxed : System.Exception { public void Own() { } } }";
        TypeSpecifier exception = TypeSpecifier.FromType<Exception>();
        MethodSpecifier getBase = new("GetBaseException", [], [exception], MethodModifiers.None, MemberVisibility.Public, exception, []);
        var core = new InMemoryTypeCatalog(
            new CatalogInfo("core", "1.0", [CoreLib]),
            [exception], [getBase], [], [],
            new Dictionary<TypeSpecifier, IReadOnlyList<string>>(),
            new Dictionary<MethodSpecifier, string>());
        var live = new ReflectionProvider(fixture.Assemblies, [new SourceFile("boxed.cs", Source)], new HashSet<string>([CoreLib]));
        var composite = new CompositeReflectionProvider([core, live]);

        var members = composite.GetMethods(new ReflectionProviderMethodQuery().WithType(Boxed)).Select(method => method.Name).ToList();

        Assert.Equal(["GetBaseException", "Own"], members.Order(StringComparer.Ordinal));
        Assert.Null(composite.GetHidingCatalogId(Boxed));
        Assert.Null(composite.GetHidingCatalogId(exception));
        Assert.Equal("core", composite.GetHidingCatalogId(TypeSpecifier.FromType<string>()));
    }

    [Fact]
    public void InMemoryCatalogFiltersMethodsAndConstructorsByType()
    {
        MethodSpecifier spawn = Method("Spawn", Widget);
        MethodSpecifier build = Method("Build", Gadget);
        var constructor = new ConstructorSpecifier([], Widget);
        var catalog = new InMemoryTypeCatalog(
            new CatalogInfo("acme", "1.0", ["Acme"]),
            [Widget, Gadget],
            [spawn, build],
            [],
            [constructor],
            new Dictionary<TypeSpecifier, IReadOnlyList<string>> { [Widget] = ["Big", "Small"] },
            new Dictionary<MethodSpecifier, string>());

        Assert.Equal([spawn], catalog.GetMethods(new ReflectionProviderMethodQuery().WithType(Widget)));
        Assert.Equal([spawn, build], catalog.GetMethods(new ReflectionProviderMethodQuery().WithStatic(true)));
        Assert.Empty(catalog.GetMethods(new ReflectionProviderMethodQuery().WithStatic(false)));
        Assert.Equal([constructor], catalog.GetConstructors(Widget));
        Assert.Empty(catalog.GetConstructors(Gadget));
        Assert.Equal(["Big", "Small"], catalog.GetEnumNames(Widget));
        Assert.Empty(catalog.GetEnumNames(Gadget));
    }
}
