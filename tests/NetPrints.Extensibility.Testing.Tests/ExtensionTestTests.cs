using NetPrints.Core;
using NetPrints.Extensibility.Loading;

namespace NetPrints.Extensibility.Testing.Tests;

/// <summary>FR-076: <c>ExtensionTest&lt;T&gt;</c> drives a real extension from a declarative state.</summary>
public sealed class ExtensionTestTests : ExtensionTest<MinimalExtension>
{
    [Fact]
    public async Task ADeclaredNodeKindIsTranslatedWrittenAndReadBack()
    {
        State.ExtensionId = MinimalExtension.Id;
        State.NodeKinds.Add(MinimalExtension.PingKind);

        ExtensionTestResult result = await RunAsync(TestContext.Current.CancellationToken);

        Assert.Empty(result.LoadFailures);
        Assert.Contains(result.Registry.Loaded, manifest => manifest.Id == MinimalExtension.Id);
        Assert.Contains(MinimalExtension.PingText, result.TranslatedCode, StringComparison.Ordinal);
        Assert.NotNull(result.Document);
        Assert.Contains(MinimalExtension.PingKind, System.Text.Encoding.UTF8.GetString(result.Document), StringComparison.Ordinal);
        Assert.Equal(result.Document, result.RoundTrippedDocument);
    }

    [Fact]
    public async Task WithoutNodeKindsTheExtensionIsOnlyLoaded()
    {
        State.ExtensionId = MinimalExtension.Id;

        ExtensionTestResult result = await RunAsync(TestContext.Current.CancellationToken);

        Assert.Empty(result.LoadFailures);
        Assert.Null(result.TranslatedCode);
        Assert.Null(result.Document);
    }

    [Fact]
    public async Task AFolderBasedExtensionLoadsNextToTheRealOne()
    {
        State.ExtensionId = MinimalExtension.Id;
        State.ExtensionFolders.Add(KitFixtures.AlphaFolder());

        ExtensionTestResult result = await RunAsync(TestContext.Current.CancellationToken);

        Assert.Empty(result.LoadFailures);
        Assert.Contains(result.Registry.Loaded, manifest => manifest.Id == MinimalExtension.Id);
        Assert.Contains(result.Registry.Loaded, manifest => manifest.Id == KitFixtures.Alpha);
    }

    [Fact]
    public async Task ANodeKindThatIsNotLoadedIsReported()
    {
        State.ExtensionId = MinimalExtension.Id;
        State.NodeKinds.Add("test.minimal/Missing");

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() => RunAsync(TestContext.Current.CancellationToken));

        Assert.Contains("test.minimal/Missing", error.Message, StringComparison.Ordinal);
    }
}

/// <summary>The harness loads a real and a folder-based extension and drives the pipeline with both.</summary>
public sealed class ExtensionHarnessTests
{
    [Fact]
    public async Task ARealAndAFolderBasedExtensionLoadNextToTheBuiltInOne()
    {
        string api = $"{ExtensionApi.Version.Major}.{ExtensionApi.Version.Minor}";
        var manifest = new ExtensionManifest(MinimalExtension.Id, "Minimal", "1.0.0", "Minimal.dll", api, []);

        await using ExtensionHarness harness = await ExtensionHarness.CreateWithManifestsAsync(
            [KitFixtures.AlphaFolder()], [(manifest, new MinimalExtension())], TestContext.Current.CancellationToken);

        Assert.Empty(harness.Registry.Results.OfType<ExtensionLoadResult.Failed>());
        Assert.Equal(3, harness.Registry.Loaded.Count);
        ClassGraph graph = harness.BuildClass("Kit", "Both", MinimalExtension.PingKind, KitFixtures.AlphaKind);
        string code = harness.Translate(graph);
        Assert.Contains(MinimalExtension.PingText, code, StringComparison.Ordinal);
        Assert.Contains("fx.alpha ping", code, StringComparison.Ordinal);
        byte[] document = await harness.WriteAsync(graph, TestContext.Current.CancellationToken);
        Assert.Equal(document, await harness.RoundTripAsync(document, TestContext.Current.CancellationToken));
    }
}
