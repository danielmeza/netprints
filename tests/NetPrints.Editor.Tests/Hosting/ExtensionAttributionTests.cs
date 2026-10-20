using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Editor.Hosting;
using NetPrints.Extensibility.Loading;

namespace NetPrints.Editor.Tests.Hosting;

/// <summary>R3-1: an exception thrown from an extension's code is attributed to the extension by the assembly of its stack frames.</summary>
public sealed class ExtensionAttributionTests
{
    private static readonly string ThisAssembly = typeof(ExtensionAttributionTests).Assembly.GetName().Name ?? string.Empty;

    private static ExtensionOrigin Origin(string assemblyName, string id = "fx.one") =>
        new(id, "Fixture one", assemblyName, Path.Combine("extensions", id));

    private static Func<string, ExtensionOrigin?> SoleHolder(params (string Name, ExtensionOrigin Origin)[] holders) =>
        name => holders.Where(holder => holder.Name == name).Select(holder => holder.Origin).Distinct().ToList() is [var only] ? only : null;

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
        var empty = new AssemblyLoadContext("fx.empty", isCollectible: true);
        try
        {
            Exception exception = ThrownBy(LoadLib(context));
            var inContext = new ExtensionAttribution(() => [new ExtensionOrigin("fx.one", "Fixture one", "Fx.Main", Path.Combine("extensions", "fx.one"), Context: context)]);
            var elsewhere = new ExtensionAttribution(() => [new ExtensionOrigin("fx.one", "Fixture one", "Fx.Main", Path.Combine("extensions", "fx.one"), Context: empty)]);

            Assert.Equal("fx.one", inContext.Find(exception)?.Id);
            Assert.Null(elsewhere.Find(exception));
            Assert.Null(new ExtensionAttribution(() => [Origin("Fx.Main")]).Find(exception));
        }
        finally
        {
            context.Unload();
            empty.Unload();
        }
    }

    [Fact]
    public void ALibraryAnUnlistedContextAlsoHoldsNamesNone()
    {
        var listed = new AssemblyLoadContext("fx.listed", isCollectible: true);
        var unlisted = new AssemblyLoadContext("fx.unlisted", isCollectible: true);
        try
        {
            LoadLib(listed);
            Exception exception = ThrownBy(LoadLib(unlisted));
            var attribution = new ExtensionAttribution(() => [new ExtensionOrigin("a", "Fixture a", "Fx.Main", Path.Combine("extensions", "a"), Context: listed)]);

            Assert.Null(attribution.Find(exception));
        }
        finally
        {
            listed.Unload();
            unlisted.Unload();
        }
    }

    [Fact]
    public void AMainFrameOfAnInnerExceptionBeatsALibraryFrameOfTheOuterOneThroughFind()
    {
        var context = new AssemblyLoadContext("fx.listed", isCollectible: true);
        try
        {
            Exception outer = ThrownBy(LoadLib(context), Thrown(new ArgumentException("inner")));
            var a = new ExtensionOrigin("a", "Fixture a", "Fx.Main", Path.Combine("extensions", "a"), Context: context);
            var attribution = new ExtensionAttribution(() => [a, Origin(ThisAssembly, "b")]);

            Assert.Equal("b", attribution.Find(outer)?.Id);
        }
        finally
        {
            context.Unload();
        }
    }

    private static Assembly LoadLib(AssemblyLoadContext context)
    {
        SyntaxTree tree = CSharpSyntaxTree.ParseText("namespace Fx { public static class Lib { public static void Throw(System.Exception inner) { throw new System.InvalidOperationException(\"boom\", inner); } } }");
        var compilation = CSharpCompilation.Create(
            "Fx.Lib",
            [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream = new MemoryStream();
        Assert.True(compilation.Emit(stream).Success);
        stream.Position = 0;
        return context.LoadFromStream(stream);
    }

    private static Exception ThrownBy(Assembly lib, Exception? inner = null)
    {
        MethodInfo method = lib.GetType("Fx.Lib")?.GetMethod("Throw") ?? throw new InvalidOperationException("The library has no Throw method.");
        try
        {
            method.Invoke(null, [inner]);
        }
        catch (TargetInvocationException failure) when (failure.InnerException is not null)
        {
            return failure.InnerException;
        }

        throw new InvalidOperationException("The library did not throw.");
    }

    [Fact]
    public void LibraryFramesThatNameTwoExtensionsNameNone()
    {
        ExtensionOrigin a = Origin("AMain", "a");
        ExtensionOrigin dep = Origin("DepMain", "dep");

        ExtensionOrigin? decided = ExtensionAttribution.Decide(["Lib", "AViews"], [a, dep], SoleHolder(("Lib", dep), ("AViews", a)));

        Assert.Null(decided);
    }

    [Fact]
    public void LibraryFramesThatAllNameOneExtensionNameIt()
    {
        ExtensionOrigin a = Origin("AMain", "a");

        ExtensionOrigin? decided = ExtensionAttribution.Decide(["Lib", "AViews"], [a], SoleHolder(("Lib", a), ("AViews", a)));

        Assert.Equal("a", decided?.Id);
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

        ExtensionOrigin? decided = ExtensionAttribution.Decide(["Lib", "BMain"], [a, b], SoleHolder(("AMain", a), ("Lib", a), ("BMain", b), ("Lib", b)));

        Assert.Equal("b", decided?.Id);
    }

    [Fact]
    public void TheCallerIsNamedWhenItsLibraryComesFromADependency()
    {
        ExtensionOrigin dep = Origin("DepMain", "dep");
        ExtensionOrigin user = Origin("UserMain", "user");

        ExtensionOrigin? decided = ExtensionAttribution.Decide(["Lib", "UserMain"], [dep, user], SoleHolder(("DepMain", dep), ("Lib", dep), ("UserMain", user)));

        Assert.Equal("user", decided?.Id);
    }

    [Fact]
    public void ALibraryOnlyOneExtensionHoldsNamesItWhenNoMainFrameIsOnTheStack()
    {
        ExtensionOrigin a = Origin("AMain", "a");
        ExtensionOrigin b = Origin("BMain", "b");

        ExtensionOrigin? decided = ExtensionAttribution.Decide(["Lib", "Host"], [a, b], SoleHolder(("AMain", a), ("Lib", a), ("BMain", b)));

        Assert.Equal("a", decided?.Id);
    }

    [Fact]
    public void ALibrarySeveralExtensionsHoldNamesNoneWhenNoMainFrameIsOnTheStack()
    {
        ExtensionOrigin a = Origin("AMain", "a");
        ExtensionOrigin b = Origin("BMain", "b");

        ExtensionOrigin? decided = ExtensionAttribution.Decide(["Lib", "Host"], [a, b], SoleHolder(("AMain", a), ("Lib", a), ("BMain", b), ("Lib", b)));

        Assert.Null(decided);
    }

    [Fact]
    public void AMainFrameOfAnInnerExceptionBeatsALibraryFrameOfTheOuterOne()
    {
        ExtensionOrigin a = Origin("AMain", "a");
        ExtensionOrigin b = Origin("BMain", "b");

        ExtensionOrigin? decided = ExtensionAttribution.Decide(["Lib", "Host", "BMain"], [a, b], SoleHolder(("AMain", a), ("Lib", a), ("BMain", b)));

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
        Assert.DoesNotContain("Trusted projects", text, StringComparison.Ordinal);
        Assert.DoesNotContain("extension folders", text, StringComparison.Ordinal);
        Assert.DoesNotContain("disable", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("boom", text, StringComparison.Ordinal);
    }

    [Fact]
    public void TheMessageForAProjectsCopyKeepsTheFolderAndPointsAtTrust()
    {
        ExtensionOrigin origin = Origin(ThisAssembly) with { ProjectCopy = true };
        string text = ExtensionAttribution.Describe(origin, new InvalidOperationException("boom"));

        Assert.Contains("Fixture one", text, StringComparison.Ordinal);
        Assert.Contains(origin.Folder, text, StringComparison.Ordinal);
        Assert.Contains("keep it", text, StringComparison.Ordinal);
        Assert.Contains("Trusted projects", text, StringComparison.Ordinal);
        Assert.Contains("Don't load", text, StringComparison.Ordinal);
        Assert.DoesNotContain("move or delete", text, StringComparison.Ordinal);
        Assert.Contains("boom", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ForMarksTheExtensionsLoadedFromAProjectFolderAsProjectCopies()
    {
        string userFolder = TestExtensionFolder.Folder;
        string projectFolder = TestExtensionFolder.FixtureFolder("Fx.Editor", "fx.editor");
        await using var host = new ExtensionHost(ExtensionLoaderOptions.BuiltInOnly with { ExtensionFolders = [userFolder] }, NullLoggerFactory.Instance);
        await host.LoadForProjectAsync([projectFolder], TestContext.Current.CancellationToken);
        var attribution = ExtensionAttribution.For(host, () => [projectFolder]);

        IReadOnlyList<ExtensionOrigin> origins = attribution.Origins();

        Assert.True(origins.Single(origin => origin.Id == "fx.editor").ProjectCopy);
        Assert.False(origins.Single(origin => origin.Id == "netprints.test").ProjectCopy);
    }
}
