using NetPrints.Core;
using NetPrints.Reflection;

namespace NetPrints.Editor.Tests.Reflection;

[TestClass]
public class ReflectionProviderTests
{
    private static IReflectionProvider provider = null!;

    [ClassInitialize]
    public static void Init(TestContext _)
    {
        // Same resolution as the editor: a default project references the .NET Framework
        // reference assemblies, which fall back to the runtime assemblies on Linux.
        var project = Project.CreateNew("Test", "Test");
        var warnings = new List<string>();
        var paths = new ReferenceAssemblyResolver().ResolveAssemblyPaths(project.References.OfType<AssemblyReference>(), warnings);
        Assert.IsEmpty(warnings);
        provider = new ReflectionProvider(paths, [], []);
    }

    [TestMethod]
    public void ReturnsNonStaticTypes()
    {
        var types = provider.GetNonStaticTypes().ToList();
        Assert.IsGreaterThan(4000, types.Count);
        Assert.Contains(TypeSpecifier.FromType<string>(), types);
    }

    [TestMethod]
    public void EnumeratesStaticMethodsIncludingRefReadonlyParameters()
    {
        // Threw KeyNotFoundException (RefKind.RefReadOnlyParameter) before the mapping was added.
        var methods = provider.GetMethods(new ReflectionProviderMethodQuery() { Static = true }).ToList();
        Assert.IsGreaterThan(10000, methods.Count);
    }

    [TestMethod]
    public void StringHasInstanceMethods()
    {
        var methods = provider.GetMethods(new ReflectionProviderMethodQuery()
        {
            Type = TypeSpecifier.FromType<string>(),
            Static = false,
        }).ToList();
        Assert.IsGreaterThan(100, methods.Count);
    }

    [TestMethod]
    public void FindsConsoleWriteLineOverloads()
    {
        var writeLine = provider.GetMethods(new ReflectionProviderMethodQuery()
        {
            Type = TypeSpecifier.FromType(typeof(Console)),
            Static = true,
        }).First(m => m.Name == "WriteLine");

        var overloads = provider.GetPublicMethodOverloads(writeLine).ToList();
        Assert.IsGreaterThan(10, overloads.Count);
        Assert.IsTrue(overloads.Any(o => o.Parameters.Count == 1 && o.Parameters[0].Value == TypeSpecifier.FromType<string>()));
    }

    [TestMethod]
    public void FindsConstructors()
    {
        var constructors = provider.GetConstructors(TypeSpecifier.FromType<List<int>>()).ToList();
        Assert.IsNotEmpty(constructors);
    }

    [TestMethod]
    public void MemoizedProviderReturnsEqualResults()
    {
        var memoized = new MemoizedReflectionProvider(provider);
        var type = TypeSpecifier.FromType<List<int>>();
        static List<string> Signatures(IEnumerable<ConstructorSpecifier> ctors) =>
            ctors.Select(c => string.Join(",", c.Arguments.Select(a => a.Value.ToString()))).ToList();

        CollectionAssert.AreEqual(Signatures(provider.GetConstructors(type)), Signatures(memoized.GetConstructors(type)));
        // Second call is served from the memoization cache.
        CollectionAssert.AreEqual(Signatures(provider.GetConstructors(type)), Signatures(memoized.GetConstructors(type)));
        Assert.AreEqual(provider.TypeSpecifierIsSubclassOf(TypeSpecifier.FromType<string>(), TypeSpecifier.FromType<object>()),
            memoized.TypeSpecifierIsSubclassOf(TypeSpecifier.FromType<string>(), TypeSpecifier.FromType<object>()));
    }

    [TestMethod]
    public void MissingAssemblyPathsAreSkipped()
    {
        string missing = Path.Combine(Path.GetTempPath(), "netprints-missing-" + Guid.NewGuid() + ".dll");
        var p = new ReflectionProvider([typeof(object).Assembly.Location, missing], [], []);
        Assert.IsNotNull(p.GetNonStaticTypes().FirstOrDefault());
    }

    [TestMethod]
    public void ReflectionLibraryReferencesNoUiFramework()
    {
        var referenced = typeof(ReflectionProvider).Assembly.GetReferencedAssemblies().Select(a => a.Name!).ToList();
        Assert.IsFalse(referenced.Any(n => n.StartsWith("Avalonia", StringComparison.Ordinal)
            || n == "PresentationFramework" || n == "PresentationCore" || n == "WindowsBase"
            || n.StartsWith("System.Windows", StringComparison.Ordinal)), string.Join(", ", referenced));
    }
}
