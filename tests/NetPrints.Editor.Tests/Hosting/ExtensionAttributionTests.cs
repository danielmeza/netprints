using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using NetPrints.Editor.Hosting;

namespace NetPrints.Editor.Tests.Hosting;

/// <summary>R3-1: an exception thrown from an extension's code is attributed to the extension by the assembly of its stack frames.</summary>
public sealed class ExtensionAttributionTests
{
    private static readonly string ThisAssembly = typeof(ExtensionAttributionTests).Assembly.GetName().Name ?? string.Empty;

    private static ExtensionOrigin Origin(string assemblyName, string id = "fx.one") =>
        new(id, "Fixture one", assemblyName, Path.Combine("extensions", id));

    private static Func<ExtensionOrigin, string, bool> Held(Dictionary<string, string[]> held) =>
        (origin, name) => held.TryGetValue(origin.Id, out string[]? names) && names.Contains(name, StringComparer.Ordinal);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Exception Thrown(Exception exception)
    {
        try
        {
            throw exception;
        }
        catch (Exception caught)
        {
            return caught;
        }
    }

    [Fact]
    public void AnExceptionThrownFromAnExtensionsAssemblyIsAttributedToIt()
    {
        var attribution = new ExtensionAttribution(() => [Origin(ThisAssembly)]);

        ExtensionOrigin? found = attribution.Find(Thrown(new InvalidOperationException("boom")));

        Assert.Equal("fx.one", found?.Id);
    }

    [Fact]
    public void AnExceptionFromOtherAssembliesIsNotAttributed()
    {
        var attribution = new ExtensionAttribution(() => [Origin("Some.Other.Assembly")]);

        Assert.Null(attribution.Find(Thrown(new InvalidOperationException("boom"))));
    }

    [Fact]
    public void AnExceptionWithoutAStackTraceIsNotAttributed()
    {
        var attribution = new ExtensionAttribution(() => [Origin(ThisAssembly)]);

        Assert.Null(attribution.Find(new InvalidOperationException("never thrown")));
    }

    [Fact]
    public void AnInnerExceptionIsChecked()
    {
        var attribution = new ExtensionAttribution(() => [Origin(ThisAssembly)]);
        var outer = new InvalidOperationException("outer", Thrown(new ArgumentException("inner")));

        Assert.Equal("fx.one", attribution.Find(outer)?.Id);
    }

    [Fact]
    public void AnAggregateExceptionChecksItsInnerExceptionsInOrder()
    {
        var attribution = new ExtensionAttribution(() => [Origin(ThisAssembly, "fx.one")]);
        var aggregate = new AggregateException(new ArgumentException("not thrown"), Thrown(new ArgumentException("thrown")));

        Assert.Equal("fx.one", attribution.Find(aggregate)?.Id);
    }

    [Fact]
    public void AnExceptionFromADependencyLoadedInTheExtensionsContextIsAttributedToIt()
    {
        var context = new AssemblyLoadContext("fx.dependency", isCollectible: true);
        try
        {
            Assembly dependency = context.LoadFromAssemblyPath(typeof(ExtensionAttributionTests).Assembly.Location);
            MethodInfo thrown = dependency.GetType(typeof(ExtensionAttributionTests).FullName ?? string.Empty)?.GetMethod(nameof(Thrown), BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException("The copy has no Thrown method.");
            Exception exception = Assert.IsAssignableFrom<Exception>(thrown.Invoke(null, [new InvalidOperationException("boom")]));
            var inContext = new ExtensionAttribution(() => [new ExtensionOrigin("fx.one", "Fixture one", "Fx.Main", Path.Combine("extensions", "fx.one"), context)]);
            var elsewhere = new ExtensionAttribution(() => [new ExtensionOrigin("fx.one", "Fixture one", "Fx.Main", Path.Combine("extensions", "fx.one"), new AssemblyLoadContext("fx.empty", isCollectible: true))]);

            Assert.Equal("fx.one", inContext.Find(exception)?.Id);
            Assert.Null(elsewhere.Find(exception));
            Assert.Null(new ExtensionAttribution(() => [Origin("Fx.Main")]).Find(exception));
        }
        finally
        {
            context.Unload();
        }
    }

    [Fact]
    public void TheFirstOriginListedWinsWhenTwoShareTheAssemblyName()
    {
        var attribution = new ExtensionAttribution(() => [Origin(ThisAssembly, "fx.current"), Origin(ThisAssembly, "fx.stale")]);

        Assert.Equal("fx.current", attribution.Find(Thrown(new InvalidOperationException("boom")))?.Id);
    }

    [Fact]
    public void AMainAssemblyFrameDecidesBeforeALibraryAnotherExtensionAlsoHolds()
    {
        ExtensionOrigin a = Origin("AMain", "a");
        ExtensionOrigin b = Origin("BMain", "b");

        ExtensionOrigin? decided = ExtensionAttribution.Decide(["Lib", "BMain"], [a, b], Held(new() { ["a"] = ["AMain", "Lib"], ["b"] = ["BMain", "Lib"] }));

        Assert.Equal("b", decided?.Id);
    }

    [Fact]
    public void TheCallerIsNamedWhenItsLibraryComesFromADependency()
    {
        ExtensionOrigin dep = Origin("DepMain", "dep");
        ExtensionOrigin user = Origin("UserMain", "user");

        ExtensionOrigin? decided = ExtensionAttribution.Decide(["Lib", "UserMain"], [dep, user], Held(new() { ["dep"] = ["DepMain", "Lib"], ["user"] = ["UserMain"] }));

        Assert.Equal("user", decided?.Id);
    }

    [Fact]
    public void ALibraryOnlyOneExtensionHoldsNamesItWhenNoMainFrameIsOnTheStack()
    {
        ExtensionOrigin a = Origin("AMain", "a");
        ExtensionOrigin b = Origin("BMain", "b");

        ExtensionOrigin? decided = ExtensionAttribution.Decide(["Lib", "Host"], [a, b], Held(new() { ["a"] = ["AMain", "Lib"], ["b"] = ["BMain"] }));

        Assert.Equal("a", decided?.Id);
    }

    [Fact]
    public void ALibrarySeveralExtensionsHoldNamesNoneWhenNoMainFrameIsOnTheStack()
    {
        ExtensionOrigin a = Origin("AMain", "a");
        ExtensionOrigin b = Origin("BMain", "b");

        ExtensionOrigin? decided = ExtensionAttribution.Decide(["Lib", "Host"], [a, b], Held(new() { ["a"] = ["AMain", "Lib"], ["b"] = ["BMain", "Lib"] }));

        Assert.Null(decided);
    }

    [Fact]
    public void AMainFrameOfAnInnerExceptionBeatsALibraryFrameOfTheOuterOne()
    {
        ExtensionOrigin a = Origin("AMain", "a");
        ExtensionOrigin b = Origin("BMain", "b");

        ExtensionOrigin? decided = ExtensionAttribution.Decide(["Lib", "Host", "BMain"], [a, b], Held(new() { ["a"] = ["AMain", "Lib"], ["b"] = ["BMain"] }));

        Assert.Equal("b", decided?.Id);
    }

    [Fact]
    public void TheMessageNamesTheExtensionAndWhatTheUserCanDoNow()
    {
        ExtensionOrigin origin = Origin(ThisAssembly);
        string text = ExtensionAttribution.Describe(origin, new InvalidOperationException("boom"));

        Assert.Contains("Fixture one", text, StringComparison.Ordinal);
        Assert.Contains("fx.one", text, StringComparison.Ordinal);
        Assert.Contains("log", text, StringComparison.Ordinal);
        Assert.Contains(origin.Folder, text, StringComparison.Ordinal);
        Assert.Contains("move or delete that folder", text, StringComparison.Ordinal);
        Assert.Contains("restart NetPrints", text, StringComparison.Ordinal);
        Assert.Contains("trusting the project", text, StringComparison.Ordinal);
        Assert.DoesNotContain("extension folders", text, StringComparison.Ordinal);
        Assert.DoesNotContain("disable", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("boom", text, StringComparison.Ordinal);
    }
}
