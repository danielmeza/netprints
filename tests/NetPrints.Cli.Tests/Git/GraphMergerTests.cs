using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NetPrints.Cli.Infrastructure;
using NetPrints.Cli.Tests.Support;
using NetPrints.Compilation;
using NetPrints.Generation;
using NetPrints.Projects;
using NetPrints.Serialization;
using NetPrints.Testing;
using Xunit;

namespace NetPrints.Cli.Tests.Git;

/// <summary>GI-T03 to GI-T08 and GI-T12: the <c>merge</c> driver merges graphs by identity and falls back to a text merge with conflict markers.</summary>
public sealed class GraphMergerTests : IDisposable
{
    private static readonly string FixtureRoot = Path.Combine(LocalSdkLayout.FindRepositoryRoot(), "tests", "NetPrints.Cli.Tests", "Git", "Fixtures");
    private readonly string _temp = Directory.CreateTempSubdirectory("np-merge-").FullName;

    public void Dispose() => Directory.Delete(_temp, recursive: true);

    private static string Fixture(string fixture, string file) => Path.Combine(FixtureRoot, fixture, file + ".netpc.json");

    private sealed record MergeRun(int Exit, string Merged, string Error);

    /// <summary>Runs <c>merge</c> the way git does: three extension-less temporary files, the result written over the second.</summary>
    private async Task<MergeRun> MergeAsync(string fixture, params string[] extra)
    {
        string baseFile = Path.Combine(_temp, "O.tmp");
        string oursFile = Path.Combine(_temp, "A.tmp");
        string theirsFile = Path.Combine(_temp, "B.tmp");
        File.Copy(Fixture(fixture, "base"), baseFile, overwrite: true);
        string oursFixture = Fixture(fixture, "ours");
        File.Copy(File.Exists(oursFixture) ? oursFixture : Path.Combine(FixtureRoot, fixture, "ours.conflicted.txt"), oursFile, overwrite: true);
        File.Copy(Fixture(fixture, "theirs"), theirsFile, overwrite: true);

        var host = new CliTestHost(_temp);
        string[] args = ["merge", baseFile, oursFile, theirsFile, "--path", "samples/C.netpc.json", .. extra];
        int exit = await host.RunRealAsync(args);
        return new MergeRun(exit, File.ReadAllText(oursFile), host.Error.ToString());
    }

    private static async Task<string> CanonicalAsync(string path)
    {
        IDocumentFormat format = GraphFormats.CreateRegistry().Default;
        await using FileStream input = File.OpenRead(path);
        var document = await format.ReadClassAsync(input, new DocumentId("C.netpc.json"), TestContext.Current.CancellationToken);
        using var output = new MemoryStream();
        await format.WriteClassAsync(document, output, TestContext.Current.CancellationToken);
        return System.Text.Encoding.UTF8.GetString(output.ToArray());
    }

    [Fact]
    public async Task TwoBranchesAddingDifferentNodesMergeCleanlyIntoTheCanonicalUnionWhereGitMergeFileConflicts()
    {
        MergeRun run = await MergeAsync("MergeClean");

        Assert.True(run.Exit == ExitCodes.Success, run.Error);
        Assert.Equal(await CanonicalAsync(Fixture("MergeClean", "expected")), run.Merged);
        Assert.Equal(File.ReadAllText(Fixture("MergeClean", "expected")), run.Merged);

        ProcessResult plain = await new ProcessRunner().RunAsync(
            new ProcessStartRequest("git", ["merge-file", "-p", Fixture("MergeClean", "ours"), Fixture("MergeClean", "base"), Fixture("MergeClean", "theirs")], _temp),
            TestContext.Current.CancellationToken);
        Assert.True(plain.ExitCode > 0, "A plain text merge of the same inputs is expected to conflict.");
    }

    [Fact]
    public async Task ThePinChangedDifferentlyOnBothSidesExitsOneWithMarkersAndBothValues()
    {
        MergeRun run = await MergeAsync("PinConflict", "--marker-size", "9");

        Assert.Equal(ExitCodes.Failed, run.Exit);
        Assert.Contains("<<<<<<<<< ours", run.Merged, StringComparison.Ordinal);
        Assert.Contains(">>>>>>>>> theirs", run.Merged, StringComparison.Ordinal);
        Assert.Contains("ours-value", run.Merged, StringComparison.Ordinal);
        Assert.Contains("theirs-value", run.Merged, StringComparison.Ordinal);
        Assert.Contains("PinValue", run.Error, StringComparison.Ordinal);
        Assert.Contains("n0000000000002", run.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ANodeDeletedOnOneSideAndChangedOnTheOtherFallsBackToTheTextMerge()
    {
        MergeRun run = await MergeAsync("DeleteModify");

        Assert.Equal(ExitCodes.Failed, run.Exit);
        Assert.Contains("<<<<<<< ours", run.Merged, StringComparison.Ordinal);
        Assert.Contains("DeleteModify", run.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("DataInputTwice", "DataInputTwice")]
    [InlineData("DanglingConnection", "DanglingConnection")]
    public async Task ASemanticConflictOfTheMergedGraphFallsBackToTheTextMergeAndExitsOne(string fixture, string kind)
    {
        MergeRun run = await MergeAsync(fixture);

        Assert.Equal(ExitCodes.Failed, run.Exit);
        Assert.Contains(kind, run.Error, StringComparison.Ordinal);
        Assert.Contains("n0000000000010", run.Merged, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnreadableInputIsMergedAsRawTextAndExitsOne()
    {
        MergeRun run = await MergeAsync("Unreadable");

        Assert.Equal(ExitCodes.Failed, run.Exit);
        Assert.Contains("<<<<<<< HEAD", run.Merged, StringComparison.Ordinal);
        Assert.Contains("\"name\": \"Entry\"", run.Merged, StringComparison.Ordinal);
        Assert.Contains("unreadable", run.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BothSidesMovingTheSameNodeKeepOursPositionAndTakeTheOtherSidesEdit()
    {
        MergeRun run = await MergeAsync("BothMove");

        Assert.True(run.Exit == ExitCodes.Success, run.Error);
        Assert.Equal(await CanonicalAsync(Fixture("BothMove", "expected")), run.Merged);
        Assert.Equal(File.ReadAllText(Fixture("BothMove", "expected")), run.Merged);
    }

    [Fact]
    public async Task TheMergedGraphGeneratesTheSameCSharpAsTheHandMergedReference()
    {
        MergeRun run = await MergeAsync("MergeClean");
        Assert.True(run.Exit == ExitCodes.Success, run.Error);

        string merged = await GenerateAsync("merged", run.Merged);
        string reference = await GenerateAsync("reference", File.ReadAllText(Fixture("MergeClean", "reference")));

        Assert.Equal(reference, merged);
        Assert.Contains("Beep", merged, StringComparison.Ordinal);
        Assert.Contains("WriteLine", merged, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AMissingInputFileIsAUsageError()
    {
        var host = new CliTestHost(_temp);

        int exit = await host.RunRealAsync("merge", Fixture("MergeClean", "base"), Path.Combine(_temp, "missing.tmp"), Fixture("MergeClean", "theirs"));

        Assert.Equal(ExitCodes.Usage, exit);
        Assert.Contains("does not exist", host.Error.ToString(), StringComparison.Ordinal);
    }

    private async Task<string> GenerateAsync(string directoryName, string graph)
    {
        string directory = Directory.CreateDirectory(Path.Combine(_temp, directoryName)).FullName;
        string input = Path.Combine(directory, "C.netpc.json");
        string output = Path.Combine(directory, "C.netpc.g.cs");
        await File.WriteAllTextAsync(input, graph, TestContext.Current.CancellationToken);
        var request = new GenerateRequest(Path.Combine(directory, "P.csproj"), "T", "netprints.default", [new GraphJob(input, output)], []);
        (var registry, IReadOnlyList<CodeDiagnostic> loadDiagnostics) = GraphCodeGenerator.LoadExtensions(request, TestContext.Current.CancellationToken);
        Assert.Empty(loadDiagnostics);

        IReadOnlyList<GeneratedFileResult> results = await GraphCodeGenerator.Create(registry).GenerateAsync(request, TestContext.Current.CancellationToken);
        GeneratedFileResult result = Assert.Single(results);
        Assert.Empty(result.Diagnostics);
        return await File.ReadAllTextAsync(output, TestContext.Current.CancellationToken);
    }
}
