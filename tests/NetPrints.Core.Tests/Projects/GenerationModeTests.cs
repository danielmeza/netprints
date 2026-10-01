using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Extensibility.Loading;
using NetPrints.Generation;
using NetPrints.Serialization;
using NetPrints.Serialization.Json;
using NetPrints.Serialization.Mapping;
using NetPrints.Serialization.Migrations;
using NetPrints.Tests.Extensibility;
using NetPrints.Tests.Samples;
using Xunit;

namespace NetPrints.Tests.Projects;

/// <summary>CL-T14 (contracts/cli.md §6): <see cref="GenerationMode.Check"/> never writes, and <see cref="GeneratedFileResult.UpToDate"/> reports whether each file already matched.</summary>
public sealed class GenerationModeTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("netprints-genmode-").FullName;
    private readonly string _graph;
    private readonly string _output;
    private readonly GenerateRequest _request;

    public GenerationModeTests()
    {
        _graph = Path.Combine(_directory, "HelloWorld.Program.netpc.json");
        _output = Path.Combine(_directory, "HelloWorld.Program.netpc.g.cs");
        File.Copy(
            Path.Combine(SampleProjectFactory.FindRepositoryRoot(), "samples", "HelloWorld", "HelloWorld.Program.netpc.json"),
            _graph);
        _request = new GenerateRequest(Path.Combine(_directory, "HelloWorld.csproj"), "HelloWorld", "netprints.default", [new GraphJob(_graph, _output)], []);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private static GraphCodeGenerator NewGenerator()
    {
        var converters = new NodeDocumentConverterRegistry(NodeDocumentConverterRegistry.BuiltIn, []);
        var mapper = new DocumentMapper(converters, NullLogger<DocumentMapper>.Instance);
        var json = new JsonDocumentFormat(new NetPrintsJsonOptions(converters), new DocumentMigrator([], NullLogger<DocumentMigrator>.Instance));
        return new GraphCodeGenerator(ExtensionTestSupport.Load(ExtensionLoaderOptions.BuiltInOnly), new DocumentFormatRegistry([json]), mapper);
    }

    private async Task<GeneratedFileResult> RunAsync(GenerationMode mode)
    {
        IReadOnlyList<GeneratedFileResult> results = await NewGenerator().GenerateAsync(_request, mode, TestContext.Current.CancellationToken);
        return Assert.Single(results);
    }

    [Fact]
    public async Task CheckOfAMissingFileWritesNothingAndReportsItStale()
    {
        GeneratedFileResult result = await RunAsync(GenerationMode.Check);

        Assert.False(File.Exists(_output));
        Assert.False(result.Written);
        Assert.False(result.UpToDate);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public async Task CheckOfAStaleFileLeavesItUntouched()
    {
        await File.WriteAllTextAsync(_output, "// stale\n", TestContext.Current.CancellationToken);

        GeneratedFileResult result = await RunAsync(GenerationMode.Check);

        Assert.Equal("// stale\n", await File.ReadAllTextAsync(_output, TestContext.Current.CancellationToken));
        Assert.False(result.Written);
        Assert.False(result.UpToDate);
    }

    [Fact]
    public async Task CheckAfterWriteReportsUpToDate()
    {
        GeneratedFileResult written = await RunAsync(GenerationMode.Write);
        Assert.True(written.Written);
        Assert.False(written.UpToDate);

        DateTime writtenAt = File.GetLastWriteTimeUtc(_output);
        GeneratedFileResult checkedResult = await RunAsync(GenerationMode.Check);

        Assert.False(checkedResult.Written);
        Assert.True(checkedResult.UpToDate);
        Assert.Equal(writtenAt, File.GetLastWriteTimeUtc(_output));
    }

    [Fact]
    public async Task WriteOfAnUpToDateFileIsUpToDateAndNotWritten()
    {
        await RunAsync(GenerationMode.Write);

        GeneratedFileResult second = await RunAsync(GenerationMode.Write);

        Assert.True(second.UpToDate);
        Assert.False(second.Written);
    }

    [Fact]
    public async Task TheDefaultOverloadWrites()
    {
        IReadOnlyList<GeneratedFileResult> results = await NewGenerator().GenerateAsync(_request, TestContext.Current.CancellationToken);

        Assert.True(Assert.Single(results).Written);
        Assert.True(File.Exists(_output));
    }

    [Fact]
    public async Task AnErrorIsNeverUpToDate()
    {
        await File.WriteAllTextAsync(_graph, "{ not json", TestContext.Current.CancellationToken);

        GeneratedFileResult result = await RunAsync(GenerationMode.Check);

        Assert.False(result.UpToDate);
        Assert.NotEmpty(result.Diagnostics);
    }
}
