using NetPrints.Core;
using NetPrints.Reflection;

namespace NetPrints.Editor.Tests.Reflection;

/// <summary>A reflection provider over the runtime assembly set, shared by the tests of a class.</summary>
public sealed class RuntimeReflectionFixture
{
    public RuntimeReflectionFixture()
    {
        // Same resolution as the editor: a default project references the .NET Framework
        // reference assemblies, which fall back to the runtime assemblies on Linux.
        var project = Project.CreateNew("Test", "Test");
        Paths = new ReferenceAssemblyResolver().ResolveAssemblyPaths(project.References.OfType<AssemblyReference>(), Warnings);
        Provider = new ReflectionProvider(Paths, [], []);
    }

    public List<string> Warnings { get; } = [];

    public IReadOnlyList<string> Paths { get; }

    public IReflectionProvider Provider { get; }
}

public class ReflectionProviderTests(RuntimeReflectionFixture fixture) : IClassFixture<RuntimeReflectionFixture>
{
    private readonly IReflectionProvider provider = fixture.Provider;

    [Fact]
    public void RuntimeReferencesResolveWithoutWarnings() => Assert.Empty(fixture.Warnings);

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
    public void MissingAssemblyPathsAreSkipped()
    {
        string missing = Path.Combine(Path.GetTempPath(), "netprints-missing-" + Guid.NewGuid() + ".dll");
        var p = new ReflectionProvider([typeof(object).Assembly.Location, missing], [], []);
        Assert.NotNull(p.GetNonStaticTypes().FirstOrDefault());
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
