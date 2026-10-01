using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NetPrints.Extensibility.Loading;
using NetPrints.Generation;
using NetPrints.Testing.Extensions;
using Xunit;

namespace NetPrints.Tests.Extensibility.MultiExtension;

/// <summary>MX-T12: a document written with both extensions survives being opened with any subset of them.</summary>
[Collection(nameof(RealExtensionLoadCollection))]
public sealed class DocumentSubsetTests : IAsyncLifetime
{
    private readonly string root = Directory.CreateTempSubdirectory("netprints-document-subset-").FullName;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        Directory.Delete(root, recursive: true);
        return ValueTask.CompletedTask;
    }

    private string[] Folders(params string[] ids) => [.. ids.Select(id => FixtureExtensions.CopyTo(root, id, id + "-" + Guid.NewGuid().ToString("N")))];

    private async Task<byte[]> WriteBothAsync()
    {
        await using ExtensionHarness both = await ExtensionHarness.CreateAsync(Folders(FixtureExtensions.Alpha, FixtureExtensions.Beta), [], TestContext.Current.CancellationToken);
        return await MultiExtensionGraphs.WriteAsync(both.Registry, MultiExtensionGraphs.BuildClass(both.Registry, "Ns", "Mixed", "fx.alpha/Ping", "fx.beta/Pong"));
    }

    [Theory]
    [InlineData("fx.alpha", "fx.beta")]
    [InlineData("fx.alpha")]
    [InlineData]
    public async Task UnknownNodesArePreservedAndTheResavedDocumentIsByteIdentical(params string[] ids)
    {
        byte[] document = await WriteBothAsync();

        await using ExtensionHarness harness = await ExtensionHarness.CreateAsync(Folders(ids), [], TestContext.Current.CancellationToken);
        byte[] saved = await harness.RoundTripAsync(document, TestContext.Current.CancellationToken);

        Assert.Equal(document, saved);
    }

    private async Task<byte[]> WriteAlphaAndLibV1Async()
    {
        await using ExtensionHarness both = await ExtensionHarness.CreateAsync(Folders(FixtureExtensions.Alpha, FixtureExtensions.LibV1), [], TestContext.Current.CancellationToken);
        return await MultiExtensionGraphs.WriteAsync(both.Registry, MultiExtensionGraphs.BuildClass(both.Registry, "Ns", "Independent", "fx.alpha/Ping", "fx.libv1/Describe"));
    }

    [Theory]
    [InlineData("fx.libv1")]
    [InlineData("fx.alpha")]
    public async Task OpeningADocumentOfTwoIndependentExtensionsWithOnlyOneKeepsTheOthersNodesByteIdentical(string only)
    {
        byte[] document = await WriteAlphaAndLibV1Async();

        await using ExtensionHarness harness = await ExtensionHarness.CreateAsync(Folders(only), [], TestContext.Current.CancellationToken);
        byte[] saved = await harness.RoundTripAsync(document, TestContext.Current.CancellationToken);

        Assert.Equal(document, saved);
    }

    [Fact]
    public async Task GeneratingWithOnlyLibV1ReportsNpt003ForAlphasNodeAndWritesNothing()
    {
        byte[] document = await WriteAlphaAndLibV1Async();
        string project = Directory.CreateDirectory(Path.Combine(root, "Proj")).FullName;
        await File.WriteAllBytesAsync(Path.Combine(project, "Independent.netpc.json"), document, TestContext.Current.CancellationToken);

        await using ExtensionHarness libOnly = await ExtensionHarness.CreateAsync(Folders(FixtureExtensions.LibV1), [], TestContext.Current.CancellationToken);
        GeneratedFileResult missing = Assert.Single(await libOnly.GenerateAsync(project, TestContext.Current.CancellationToken));

        Assert.False(missing.Written);
        Assert.Contains(missing.Diagnostics, d => d.Id == GraphCodeGenerator.MissingExtensionCode && d.Message.Contains("fx.alpha/Ping", StringComparison.Ordinal));
        Assert.DoesNotContain(missing.Diagnostics, d => d.Message.Contains("fx.libv1/Describe", StringComparison.Ordinal));
        Assert.False(File.Exists(missing.Output));
    }

    [Fact]
    public async Task GeneratingWithOnlyAlphaReportsNpt003ForBetasNodeAndWritesNothing()
    {
        byte[] document = await WriteBothAsync();
        string project = Directory.CreateDirectory(Path.Combine(root, "Proj")).FullName;
        await File.WriteAllBytesAsync(Path.Combine(project, "Mixed.netpc.json"), document, TestContext.Current.CancellationToken);

        await using ExtensionHarness alphaOnly = await ExtensionHarness.CreateAsync(Folders(FixtureExtensions.Alpha), [], TestContext.Current.CancellationToken);
        GeneratedFileResult missing = Assert.Single(await alphaOnly.GenerateAsync(project, TestContext.Current.CancellationToken));

        Assert.False(missing.Written);
        Assert.Contains(missing.Diagnostics, d => d.Id == GraphCodeGenerator.MissingExtensionCode && d.Message.Contains("fx.beta/Pong", StringComparison.Ordinal));
        Assert.False(File.Exists(missing.Output));

        await using ExtensionHarness both = await ExtensionHarness.CreateAsync(Folders(FixtureExtensions.Alpha, FixtureExtensions.Beta), [], TestContext.Current.CancellationToken);
        GeneratedFileResult generated = Assert.Single(await both.GenerateAsync(project, TestContext.Current.CancellationToken));
        Assert.True(generated.Written);
        Assert.Contains("Description(\"fx.beta\")", await File.ReadAllTextAsync(generated.Output, TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }
}
