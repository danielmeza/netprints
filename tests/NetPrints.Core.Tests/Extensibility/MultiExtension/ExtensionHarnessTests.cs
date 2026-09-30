using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NetPrints.Core;
using NetPrints.Extensibility;
using NetPrints.Extensibility.Loading;
using NetPrints.Generation;
using NetPrints.Testing.Extensions;
using Xunit;

namespace NetPrints.Tests.Extensibility.MultiExtension;

/// <summary>The in-repository extension test harness (contracts/extensions.md §4, ADR-0010 §5).</summary>
[Collection(nameof(RealExtensionLoadCollection))]
public sealed class ExtensionHarnessTests : IAsyncLifetime
{
    private const string AlphaPing = "fx.alpha/Ping";
    private const string AlphaPingLine = "System.Console.WriteLine(\"fx.alpha ping\");";
    private const string AlphaMarker = "[System.ComponentModel.Description(\"fx.alpha\")]";

    private readonly string root = Directory.CreateTempSubdirectory("netprints-harness-").FullName;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        Directory.Delete(root, recursive: true);
        return ValueTask.CompletedTask;
    }

    private string AlphaFolder() => FixtureExtensions.CopyTo(root, FixtureExtensions.Alpha);

    [Fact]
    public async Task FoldersAndInProcessExtensionsAreLoadedNextToTheBuiltInOne()
    {
        var inProcess = new DelegateExtension(builder => builder.AddProjectProperty("HarnessProperty"));

        await using ExtensionHarness harness = await ExtensionHarness.CreateAsync([AlphaFolder()], [inProcess], TestContext.Current.CancellationToken);

        Assert.Contains(harness.Registry.Loaded, m => m.Id == FixtureExtensions.Alpha);
        Assert.Equal(3, harness.Registry.Loaded.Count);
        Assert.Contains("FxAlphaProperty", harness.Registry.ProjectProperties);
        Assert.Contains("HarnessProperty", harness.Registry.ProjectProperties);
        Assert.Empty(harness.Registry.Results.OfType<ExtensionLoadResult.Failed>());
    }

    [Fact]
    public async Task TranslateUsesTheLoadedExtensionsTranslatorsAndEmitters()
    {
        await using ExtensionHarness harness = await ExtensionHarness.CreateAsync([AlphaFolder()], [], TestContext.Current.CancellationToken);
        ClassGraph cls = MultiExtensionGraphs.BuildClass(harness.Registry, "Ns", "Pings", AlphaPing);

        string code = harness.Translate(cls);

        Assert.Contains(AlphaPingLine, code, StringComparison.Ordinal);
        Assert.Contains(AlphaMarker, code, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RoundTripKeepsABytesCanonicalDocumentIdenticalWithAndWithoutTheExtension()
    {
        await using ExtensionHarness withAlpha = await ExtensionHarness.CreateAsync([AlphaFolder()], [], TestContext.Current.CancellationToken);
        byte[] document = await MultiExtensionGraphs.WriteAsync(withAlpha.Registry, MultiExtensionGraphs.BuildClass(withAlpha.Registry, "Ns", "Pings", AlphaPing));

        byte[] same = await withAlpha.RoundTripAsync(document, TestContext.Current.CancellationToken);
        await using ExtensionHarness without = await ExtensionHarness.CreateAsync([], [], TestContext.Current.CancellationToken);
        byte[] preserved = await without.RoundTripAsync(document, TestContext.Current.CancellationToken);

        Assert.Equal(document, same);
        Assert.Equal(document, preserved);
    }

    [Fact]
    public async Task GenerateWritesTheGeneratedFileOfEveryGraphInTheProjectDirectory()
    {
        await using ExtensionHarness harness = await ExtensionHarness.CreateAsync([AlphaFolder()], [], TestContext.Current.CancellationToken);
        string project = Directory.CreateDirectory(Path.Combine(root, "Proj")).FullName;
        byte[] document = await MultiExtensionGraphs.WriteAsync(harness.Registry, MultiExtensionGraphs.BuildClass(harness.Registry, "Ns", "Pings", AlphaPing));
        await File.WriteAllBytesAsync(Path.Combine(project, "Pings.netpc.json"), document, TestContext.Current.CancellationToken);

        var results = await harness.GenerateAsync(project, TestContext.Current.CancellationToken);

        GeneratedFileResult result = Assert.Single(results);
        Assert.True(result.Written);
        Assert.Contains(AlphaPingLine, await File.ReadAllTextAsync(result.Output, TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateReportsNpt003WhenTheExtensionOfAGraphIsNotLoaded()
    {
        await using ExtensionHarness withAlpha = await ExtensionHarness.CreateAsync([AlphaFolder()], [], TestContext.Current.CancellationToken);
        string project = Directory.CreateDirectory(Path.Combine(root, "Proj")).FullName;
        byte[] document = await MultiExtensionGraphs.WriteAsync(withAlpha.Registry, MultiExtensionGraphs.BuildClass(withAlpha.Registry, "Ns", "Pings", AlphaPing));
        await File.WriteAllBytesAsync(Path.Combine(project, "Pings.netpc.json"), document, TestContext.Current.CancellationToken);

        await using ExtensionHarness without = await ExtensionHarness.CreateAsync([], [], TestContext.Current.CancellationToken);
        var results = await without.GenerateAsync(project, TestContext.Current.CancellationToken);

        GeneratedFileResult result = Assert.Single(results);
        Assert.False(result.Written);
        Assert.Contains(result.Diagnostics, d => d.Id == GraphCodeGenerator.MissingExtensionCode);
    }

    [Fact]
    public async Task DisposingTheHarnessDisposesWhatTheExtensionsContributed()
    {
        var emitter = new DisposableMemberEmitter();
        var inProcess = new DelegateExtension(builder => builder.AddMemberEmitter(emitter));

        ExtensionHarness harness = await ExtensionHarness.CreateAsync([], [inProcess], TestContext.Current.CancellationToken);
        Assert.False(emitter.Disposed);
        await harness.DisposeAsync();

        Assert.True(emitter.Disposed);
    }
}
