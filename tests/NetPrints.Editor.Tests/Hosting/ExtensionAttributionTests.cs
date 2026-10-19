using System.Runtime.CompilerServices;
using NetPrints.Editor.Hosting;

namespace NetPrints.Editor.Tests.Hosting;

/// <summary>R3-1: an exception thrown from an extension's code is attributed to the extension by the assembly of its stack frames.</summary>
public class ExtensionAttributionTests
{
    private static readonly string ThisAssembly = typeof(ExtensionAttributionTests).Assembly.GetName().Name ?? string.Empty;

    private static ExtensionOrigin Origin(string assemblyName, string id = "fx.one") => new(id, "Fixture one", assemblyName);

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
    public void TheFirstOriginListedWinsWhenTwoShareTheAssemblyName()
    {
        var attribution = new ExtensionAttribution(() => [Origin(ThisAssembly, "fx.current"), Origin(ThisAssembly, "fx.stale")]);

        Assert.Equal("fx.current", attribution.Find(Thrown(new InvalidOperationException("boom")))?.Id);
    }

    [Fact]
    public void TheMessageNamesTheExtensionAndTellsToDisableIt()
    {
        string text = ExtensionAttribution.Describe(Origin(ThisAssembly), new InvalidOperationException("boom"));

        Assert.Contains("Fixture one", text, StringComparison.Ordinal);
        Assert.Contains("fx.one", text, StringComparison.Ordinal);
        Assert.Contains("Extensions page", text, StringComparison.Ordinal);
        Assert.Contains("boom", text, StringComparison.Ordinal);
    }
}
