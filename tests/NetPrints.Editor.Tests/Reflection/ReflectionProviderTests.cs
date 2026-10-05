using NetPrints.Core;
using NetPrints.Projects;
using NetPrints.Reflection;

namespace NetPrints.Editor.Tests.Reflection;

/// <summary>A reflection provider over the runtime assembly set, shared by the tests of a class.</summary>
public sealed class RuntimeReflectionFixture
{
    public RuntimeReflectionFixture()
    {
        Paths = TestSnapshots.RuntimeAssemblyPaths();
        Assemblies = Paths.Select(path => new ResolvedAssembly(path, null)).ToList();
        Provider = new ReflectionProvider(Assemblies, [], new HashSet<string>());
    }

    public IReadOnlyList<string> Paths { get; }

    public IReadOnlyList<ResolvedAssembly> Assemblies { get; }

    public IReflectionProvider Provider { get; }
}

public class ReflectionProviderTests(RuntimeReflectionFixture fixture) : IClassFixture<RuntimeReflectionFixture>
{
    private readonly IReflectionProvider provider = fixture.Provider;

    [Fact]
    public void ReturnsNonStaticTypes()
    {
        var types = provider.GetNonStaticTypes().ToList();
        Assert.True(types.Count > 4000);
        Assert.Contains(TypeSpecifier.FromType<string>(), types);
    }

    [Fact]
    public void EnumeratesStaticMethodsIncludingRefReadonlyParameters()
    {
        // Threw KeyNotFoundException (RefKind.RefReadOnlyParameter) before the mapping was added.
        var methods = provider.GetMethods(new ReflectionProviderMethodQuery() { Static = true }).ToList();
        Assert.True(methods.Count > 10000);
    }

    [Fact]
    public void StringHasInstanceMethods()
    {
        var methods = provider.GetMethods(new ReflectionProviderMethodQuery()
        {
            Type = TypeSpecifier.FromType<string>(),
            Static = false,
        }).ToList();
        Assert.True(methods.Count > 100);
    }

    [Fact]
    public void FindsConsoleWriteLineOverloads()
    {
        var writeLine = provider.GetMethods(new ReflectionProviderMethodQuery()
        {
            Type = TypeSpecifier.FromType(typeof(Console)),
            Static = true,
        }).First(m => m.Name == "WriteLine");

        var overloads = provider.GetPublicMethodOverloads(writeLine).ToList();
        Assert.True(overloads.Count > 10);
        Assert.True(overloads.Any(o => o.Parameters.Count == 1 && o.Parameters[0].Value == TypeSpecifier.FromType<string>()));
    }

    [Fact]
    public void FindsConstructors()
    {
        var constructors = provider.GetConstructors(TypeSpecifier.FromType<List<int>>()).ToList();
        Assert.NotEmpty(constructors);
    }

    [Fact]
    public void MemoizedProviderReturnsEqualResults()
    {
        var memoized = new MemoizedReflectionProvider(provider);
        var type = TypeSpecifier.FromType<List<int>>();
        static List<string> Signatures(IEnumerable<ConstructorSpecifier> ctors) =>
            ctors.Select(c => string.Join(",", c.Arguments.Select(a => a.Value.ToString()))).ToList();

        Assert.Equal(Signatures(provider.GetConstructors(type)), Signatures(memoized.GetConstructors(type)));
        // Second call is served from the memoization cache.
        Assert.Equal(Signatures(provider.GetConstructors(type)), Signatures(memoized.GetConstructors(type)));
        Assert.Equal(provider.TypeSpecifierIsSubclassOf(TypeSpecifier.FromType<string>(), TypeSpecifier.FromType<object>()), memoized.TypeSpecifierIsSubclassOf(TypeSpecifier.FromType<string>(), TypeSpecifier.FromType<object>()));
    }

    [Fact]
    public void WarmedConstructorAndOverloadQueriesAreNotConvertedAgain()
    {
        var type = TypeSpecifier.FromType<List<int>>();
        ConstructorSpecifier constructor = provider.GetConstructors(type).First();
        MethodSpecifier method = provider.GetMethods(new ReflectionProviderMethodQuery().WithStatic(true)).First();
        var spy = new ConversionSpyProvider(provider, constructor, method);
        var memoized = new MemoizedReflectionProvider(spy);

        _ = memoized.GetConstructors(type).Count();
        _ = memoized.GetPublicMethodOverloads(method).Count();
        _ = memoized.GetConstructors(type).ToList();
        _ = memoized.GetPublicMethodOverloads(method).ToList();

        Assert.Equal(1, spy.ConstructorConversions);
        Assert.Equal(1, spy.OverloadConversions);
    }

    private sealed class ConversionSpyProvider(IReflectionProvider inner, ConstructorSpecifier constructor, MethodSpecifier method) : IReflectionProvider
    {
        public int ConstructorConversions { get; private set; }

        public int OverloadConversions { get; private set; }

        public IEnumerable<ConstructorSpecifier> GetConstructors(TypeSpecifier typeSpecifier)
        {
            ConstructorConversions++;
            yield return constructor;
        }

        public IEnumerable<MethodSpecifier> GetPublicMethodOverloads(MethodSpecifier methodSpecifier)
        {
            OverloadConversions++;
            yield return method;
        }

        public bool TypeSpecifierIsSubclassOf(TypeSpecifier a, TypeSpecifier b) => inner.TypeSpecifierIsSubclassOf(a, b);

        public bool HasImplicitCast(TypeSpecifier fromType, TypeSpecifier toType) => inner.HasImplicitCast(fromType, toType);

        public IEnumerable<TypeSpecifier> GetNonStaticTypes() => inner.GetNonStaticTypes();

        public IEnumerable<MethodSpecifier> GetOverridableMethodsForType(TypeSpecifier typeSpecifier) => inner.GetOverridableMethodsForType(typeSpecifier);

        public IEnumerable<string> GetEnumNames(TypeSpecifier typeSpecifier) => inner.GetEnumNames(typeSpecifier);

        public IEnumerable<MethodSpecifier> GetMethods(ReflectionProviderMethodQuery query) => inner.GetMethods(query);

        public IEnumerable<VariableSpecifier> GetVariables(ReflectionProviderVariableQuery query) => inner.GetVariables(query);

        public string? GetMethodDocumentation(MethodSpecifier methodSpecifier) => inner.GetMethodDocumentation(methodSpecifier);

        public string? GetMethodParameterDocumentation(MethodSpecifier methodSpecifier, int parameterIndex) => inner.GetMethodParameterDocumentation(methodSpecifier, parameterIndex);

        public string? GetMethodReturnDocumentation(MethodSpecifier methodSpecifier, int returnIndex) => inner.GetMethodReturnDocumentation(methodSpecifier, returnIndex);
    }

    [Fact]
    public void MissingAssemblyPathsAreSkipped()
    {
        string missing = Path.Combine(Path.GetTempPath(), "netprints-missing-" + Guid.NewGuid() + ".dll");
        var p = new ReflectionProvider(
            [new ResolvedAssembly(typeof(object).Assembly.Location, null), new ResolvedAssembly(missing, null)],
            [], new HashSet<string>());
        Assert.NotNull(p.GetNonStaticTypes().FirstOrDefault());
    }

    // extension-points.md §4: a type catalog's covered assemblies are excluded from enumeration
    // (search/browsing) but stay referenced, so a specific, already-known type from one still resolves.
    [Fact]
    public void ExcludedAssemblyTypesAreSkippedInEnumerationButStillResolveByName()
    {
        string? coreLibName = typeof(object).Assembly.GetName().Name;
        Assert.NotNull(coreLibName);
        var provider = new ReflectionProvider(fixture.Assemblies, [], new HashSet<string> { coreLibName });

        Assert.DoesNotContain(TypeSpecifier.FromType<string>(), provider.GetNonStaticTypes().ToList());
        Assert.True(provider.TypeSpecifierIsSubclassOf(TypeSpecifier.FromType<string>(), TypeSpecifier.FromType<object>()));
    }

    [Fact]
    public void ReflectionLibraryReferencesNoUiFramework()
    {
        var referenced = typeof(ReflectionProvider).Assembly.GetReferencedAssemblies().Select(a => a.Name!).ToList();
        Assert.False(referenced.Any(n => n.StartsWith("Avalonia", StringComparison.Ordinal)
            || n == "PresentationFramework" || n == "PresentationCore" || n == "WindowsBase"
            || n.StartsWith("System.Windows", StringComparison.Ordinal)), string.Join(", ", referenced));
    }
}
