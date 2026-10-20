using System.Diagnostics;
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

    private static string Full(string simple, string version = "1.0.0.0") =>
        $"{simple}, Version={version}, Culture=neutral, PublicKeyToken=null";

    private static AssemblyHolders Held(params ExtensionOrigin[] listed) => new(listed, false);

    private static Func<string, AssemblyHolders> Holdings(params (string Identity, AssemblyHolders Holders)[] entries)
    {
        Dictionary<string, AssemblyHolders> map = entries.ToDictionary(entry => entry.Identity, entry => entry.Holders, StringComparer.Ordinal);
        return identity => map.TryGetValue(identity, out AssemblyHolders? found) ? found : new AssemblyHolders([], false);
    }

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
    public void TwoVersionsOfALibraryNameTheExtensionWhoseVersionThrew()
    {
        var unlisted = new AssemblyLoadContext("fx.unlisted", isCollectible: true);
        var listed = new AssemblyLoadContext("fx.listed", isCollectible: true);
        try
        {
            Assembly older = LoadLib(unlisted, "1.0.0.0");
            Assembly newer = LoadLib(listed, "2.0.0.0");
            var a = new ExtensionOrigin("a", "Fixture a", "Fx.Main", Path.Combine("extensions", "a"), Context: listed);
            var attribution = new ExtensionAttribution(() => [a]);

            Assert.Equal("a", attribution.Find(ThrownBy(newer))?.Id);
            Assert.Null(attribution.Find(ThrownBy(older)));
        }
        finally
        {
            unlisted.Unload();
            listed.Unload();
        }
    }

    [Fact]
    public void ACopyOfALibraryTheDefaultContextAlsoHoldsNamesNone()
    {
        var context = new AssemblyLoadContext("fx.copy", isCollectible: true);
        try
        {
            Exception exception = ThrownFromACopy(LoadThisAssemblyCopy(context));
            var a = new ExtensionOrigin("a", "Fixture a", "Fx.Main", Path.Combine("extensions", "a"), Context: context);
            var attribution = new ExtensionAttribution(() => [a]);

            Assert.Null(attribution.Find(exception));
        }
        finally
        {
            context.Unload();
        }
    }

    private static Assembly LoadThisAssemblyCopy(AssemblyLoadContext context)
    {
        using FileStream stream = File.OpenRead(typeof(ExtensionAttributionTests).Assembly.Location);
        return context.LoadFromStream(stream);
    }

    private static Exception ThrownFromACopy(Assembly copy)
    {
        MethodInfo method = copy.GetType(typeof(ExtensionAttributionTests).FullName ?? string.Empty)?.GetMethod(nameof(Thrown), BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("The copy has no Thrown method.");
        return method.Invoke(null, [new InvalidOperationException("boom")]) as Exception
            ?? throw new InvalidOperationException("The copy did not throw.");
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

    private static Assembly LoadLib(AssemblyLoadContext context, string version = "1.0.0.0")
    {
        SyntaxTree tree = CSharpSyntaxTree.ParseText($$"""
            [assembly: System.Reflection.AssemblyVersion("{{version}}")]

            namespace Fx { public static class Lib { public static void Throw(System.Exception inner) { throw new System.InvalidOperationException("boom", inner); } } }
            """);
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
    public void AFrameNamesItsAssemblyByFullNameWithTheVersion()
    {
        var context = new AssemblyLoadContext("fx.identity", isCollectible: true);
        try
        {
            Assembly lib = LoadLib(context, "1.2.3.0");
            Exception exception = ThrownBy(lib);
            string fullName = lib.FullName ?? throw new InvalidOperationException("The library has no full name.");
            string declared = DiagnosticMethodInfo.Create(new StackTrace(exception, false).GetFrames()[0])?.DeclaringAssemblyName
                ?? throw new InvalidOperationException("The frame names no assembly.");

            Assert.Contains("1.2.3.0", declared, StringComparison.Ordinal);
            Assert.Equal(new AssemblyName(fullName).FullName, new AssemblyName(declared).FullName);
        }
        finally
        {
            context.Unload();
        }
    }

    [Fact]
    public void LibraryFramesThatNameTwoExtensionsNameNone()
    {
        ExtensionOrigin a = Origin("AMain", "a");
        ExtensionOrigin dep = Origin("DepMain", "dep");

        ExtensionOrigin? decided = ExtensionAttribution.Decide([Full("Lib"), Full("AViews")], Holdings((Full("Lib"), Held(dep)), (Full("AViews"), Held(a))));

        Assert.Null(decided);
    }

    [Fact]
    public void LibraryFramesThatAllNameOneExtensionNameIt()
    {
        ExtensionOrigin a = Origin("AMain", "a");

        ExtensionOrigin? decided = ExtensionAttribution.Decide([Full("Lib"), Full("AViews")], Holdings((Full("Lib"), Held(a)), (Full("AViews"), Held(a))));

        Assert.Equal("a", decided?.Id);
    }

    [Fact]
    public void TwoListedCopiesOfOneExtensionNameTheFirst()
    {
        ExtensionOrigin first = new("fx.copy", "Fixture first", ThisAssembly, Path.Combine("extensions", "first"));
        ExtensionOrigin second = new("fx.copy", "Fixture second", ThisAssembly, Path.Combine("extensions", "second"));
        var attribution = new ExtensionAttribution(() => [first, second]);

        Assert.Same(first, attribution.Find(Thrown(new InvalidOperationException("boom"))));
    }

    [Fact]
    public void TwoExtensionsSharingAMainAssemblyNameNameNone()
    {
        var attribution = new ExtensionAttribution(() => [Origin(ThisAssembly, "fx.p"), Origin(ThisAssembly, "fx.r")]);

        Assert.Null(attribution.Find(Thrown(new InvalidOperationException("boom"))));
    }

    [Fact]
    public void AMainAssemblyFrameDecidesBeforeALibraryAnotherExtensionAlsoHolds()
    {
        ExtensionOrigin a = Origin("AMain", "a");
        ExtensionOrigin b = Origin("BMain", "b");

        ExtensionOrigin? decided = ExtensionAttribution.Decide([Full("Lib"), Full("BMain")], Holdings((Full("AMain"), Held(a)), (Full("Lib"), Held(a, b)), (Full("BMain"), Held(b))));

        Assert.Equal("b", decided?.Id);
    }

    [Fact]
    public void TheCallerIsNamedWhenItsLibraryComesFromADependency()
    {
        ExtensionOrigin dep = Origin("DepMain", "dep");
        ExtensionOrigin user = Origin("UserMain", "user");

        ExtensionOrigin? decided = ExtensionAttribution.Decide([Full("Lib"), Full("UserMain")], Holdings((Full("DepMain"), Held(dep)), (Full("Lib"), Held(dep)), (Full("UserMain"), Held(user))));

        Assert.Equal("user", decided?.Id);
    }

    [Fact]
    public void ALibraryOnlyOneExtensionHoldsNamesItWhenNoMainFrameIsOnTheStack()
    {
        ExtensionOrigin a = Origin("AMain", "a");
        ExtensionOrigin b = Origin("BMain", "b");

        ExtensionOrigin? decided = ExtensionAttribution.Decide([Full("Lib"), Full("Host")], Holdings((Full("AMain"), Held(a)), (Full("Lib"), Held(a)), (Full("BMain"), Held(b))));

        Assert.Equal("a", decided?.Id);
    }

    [Fact]
    public void ALibrarySeveralExtensionsHoldNamesNoneWhenNoMainFrameIsOnTheStack()
    {
        ExtensionOrigin a = Origin("AMain", "a");
        ExtensionOrigin b = Origin("BMain", "b");

        ExtensionOrigin? decided = ExtensionAttribution.Decide([Full("Lib"), Full("Host")], Holdings((Full("AMain"), Held(a)), (Full("Lib"), Held(a, b)), (Full("BMain"), Held(b))));

        Assert.Null(decided);
    }

    [Fact]
    public void AMainFrameOfAnInnerExceptionBeatsALibraryFrameOfTheOuterOne()
    {
        ExtensionOrigin a = Origin("AMain", "a");
        ExtensionOrigin b = Origin("BMain", "b");

        ExtensionOrigin? decided = ExtensionAttribution.Decide([Full("Lib"), Full("Host"), Full("BMain")], Holdings((Full("AMain"), Held(a)), (Full("Lib"), Held(a)), (Full("BMain"), Held(b))));

        Assert.Equal("b", decided?.Id);
    }

    [Fact]
    public void AMainFrameTwoExtensionsWithDifferentIdsHoldNamesNone()
    {
        ExtensionOrigin a = Origin("AMain", "a");
        ExtensionOrigin b = Origin("AMain", "b");

        ExtensionOrigin? decided = ExtensionAttribution.Decide([Full("AMain")], Holdings((Full("AMain"), Held(a, b))));

        Assert.Null(decided);
    }

    [Fact]
    public void AMainFrameTwoCopiesOfOneExtensionHoldNamesTheFirstListed()
    {
        ExtensionOrigin a1 = Origin("AMain", "x");
        ExtensionOrigin a2 = Origin("AMain", "x");

        ExtensionOrigin? decided = ExtensionAttribution.Decide([Full("AMain")], Holdings((Full("AMain"), Held(a1, a2))));

        Assert.Same(a1, decided);
    }

    [Fact]
    public void AMainFrameAnUnlistedContextAlsoHoldsNamesNone()
    {
        ExtensionOrigin a = Origin("AMain", "a");

        ExtensionOrigin? decided = ExtensionAttribution.Decide([Full("AMain")], Holdings((Full("AMain"), new AssemblyHolders([a], true))));

        Assert.Null(decided);
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

    [Fact]
    public async Task ForMarksAProjectFolderWrittenWithATrailingSeparatorAsTheProjectsCopy()
    {
        string userFolder = TestExtensionFolder.Folder;
        string projectFolder = TestExtensionFolder.FixtureFolder("Fx.Editor", "fx.editor") + Path.DirectorySeparatorChar;
        await using var host = new ExtensionHost(ExtensionLoaderOptions.BuiltInOnly with { ExtensionFolders = [userFolder] }, NullLoggerFactory.Instance);
        await host.LoadForProjectAsync([projectFolder], TestContext.Current.CancellationToken);
        var attribution = ExtensionAttribution.For(host, () => [projectFolder]);

        IReadOnlyList<ExtensionOrigin> origins = attribution.Origins();

        Assert.True(origins.Single(origin => origin.Id == "fx.editor").ProjectCopy);
    }
}
