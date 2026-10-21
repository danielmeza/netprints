using Microsoft.Extensions.Logging;
using NetPrints.Editor.Hosting;

namespace NetPrints.Editor.Tests.Hosting;

/// <summary>R3-4: the assembly of a loaded extension is found by the normalised path of its manifest's <c>assembly</c>.</summary>
public sealed class ExtensionAssembliesTests
{
    private static readonly string EditorAssemblyPath = typeof(ExtensionAssemblies).Assembly.Location;

    private static string ManifestPath => Path.Combine(Path.GetDirectoryName(EditorAssemblyPath) ?? string.Empty, "netprints-extension.json");

    private static string FileName => Path.GetFileName(EditorAssemblyPath);

    [Theory]
    [InlineData("")]
    [InlineData("./")]
    [InlineData("sub/../")]
    public void ADotSegmentInTheDeclaredPathStillFindsTheAssembly(string prefix)
    {
        var logger = new CollectingLogger<ExtensionContributionRecorder>();

        var found = ExtensionAssemblies.Find(ManifestPath, prefix + FileName, "fx.one", logger);

        Assert.Same(typeof(ExtensionAssemblies).Assembly, found);
        Assert.Empty(logger.Entries);
    }

    [Fact]
    public void AMissIsLoggedAsAWarningNamingTheExtensionAndThePath()
    {
        var logger = new CollectingLogger<ExtensionContributionRecorder>();

        var found = ExtensionAssemblies.Find(ManifestPath, "Nothing.Here.dll", "fx.one", logger);

        Assert.Null(found);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal(1045, entry.EventId.Id);
        Assert.Contains("fx.one", entry.Message, StringComparison.Ordinal);
        Assert.Contains("Nothing.Here.dll", entry.Message, StringComparison.Ordinal);
    }
}
