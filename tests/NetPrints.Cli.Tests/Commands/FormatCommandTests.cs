using System;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using NetPrints.Cli.Tests.Support;
using NetPrints.Testing;
using Xunit;

namespace NetPrints.Cli.Tests.Commands;

/// <summary>GI-T01: <c>format</c> rewrites non-canonical graphs, <c>--check</c> writes nothing, canonical files are never touched.</summary>
public sealed class FormatCommandTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("np-format-").FullName;
    private readonly string _canonical = File.ReadAllText(Path.Combine(LocalSdkLayout.FindRepositoryRoot(), "samples", "HelloWorld", "HelloWorld.Program.netpc.json"));
    private readonly CliTestHost _host;

    public FormatCommandTests() => _host = new CliTestHost(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Write(string relativePath, string content)
    {
        string path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? _root);
        File.WriteAllText(path, content);
        return path;
    }

    private string Compact() => JsonNode.Parse(_canonical)?.ToJsonString() ?? throw new InvalidOperationException("The sample is not JSON.");

    [Fact]
    public async Task CheckNamesANonCanonicalFileExitsOneAndWritesNothing()
    {
        string path = Write("A.netpc.json", Compact());
        byte[] before = File.ReadAllBytes(path);
        DateTime modified = File.GetLastWriteTimeUtc(path);

        int exitCode = await _host.RunAsync("format", "--check", _root);

        _host.AssertExit(ExitCodes.Failed, exitCode);
        Assert.Contains("not canonical: A.netpc.json", _host.Output, StringComparison.Ordinal);
        Assert.Contains("1 of 1 graph(s) are not canonical.", _host.Output, StringComparison.Ordinal);
        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.Equal(modified, File.GetLastWriteTimeUtc(path));
    }

    [Fact]
    public async Task FormatRewritesANonCanonicalFileAndASecondRunChangesNothing()
    {
        string path = Write("A.netpc.json", Compact());

        _host.AssertExit(ExitCodes.Success, await _host.RunAsync("format", _root));

        Assert.Equal(_canonical, File.ReadAllText(path));
        Assert.Contains("formatted: A.netpc.json", _host.Output, StringComparison.Ordinal);
        Assert.Contains("1 of 1 graph(s) formatted.", _host.Output, StringComparison.Ordinal);

        var second = new CliTestHost(_root);
        DateTime modified = File.GetLastWriteTimeUtc(path);
        second.AssertExit(ExitCodes.Success, await second.RunAsync("format", _root));
        Assert.DoesNotContain("formatted:", second.Output, StringComparison.Ordinal);
        Assert.Contains("0 of 1 graph(s) formatted.", second.Output, StringComparison.Ordinal);
        Assert.Equal(modified, File.GetLastWriteTimeUtc(path));
    }

    [Fact]
    public async Task ACanonicalFileIsLeftUntouchedBytesAndTimestamp()
    {
        string path = Write("A.netpc.json", _canonical);
        File.SetLastWriteTimeUtc(path, new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc));
        byte[] before = File.ReadAllBytes(path);
        DateTime modified = File.GetLastWriteTimeUtc(path);

        _host.AssertExit(ExitCodes.Success, await _host.RunAsync("format", _root));
        var check = new CliTestHost(_root);
        check.AssertExit(ExitCodes.Success, await check.RunAsync("format", "--check", _root));

        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.Equal(modified, File.GetLastWriteTimeUtc(path));
        Assert.Contains("1 graph(s) are canonical.", check.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnreadableFileIsReportedExitsOneAndTheOthersAreProcessed()
    {
        Write("Bad.netpc.json", "{ not json");
        string other = Write("Other.netpc.json", Compact());

        int exitCode = await _host.RunAsync("format", _root);

        _host.AssertExit(ExitCodes.Failed, exitCode);
        Assert.Contains("unreadable: Bad.netpc.json:", _host.Output, StringComparison.Ordinal);
        Assert.Contains("formatted: Other.netpc.json", _host.Output, StringComparison.Ordinal);
        Assert.Equal(_canonical, File.ReadAllText(other));
        Assert.Equal("{ not json", File.ReadAllText(Path.Combine(_root, "Bad.netpc.json")));
    }

    [Fact]
    public async Task AGraphWithANullNodeListIsUnreadable()
    {
        Write("Null.netpc.json", File.ReadAllText(Path.Combine(LocalSdkLayout.FindRepositoryRoot(), "tests", "NetPrints.Cli.Tests", "Git", "Fixtures", "NullNodes", "ours.netpc.json")));

        int exitCode = await _host.RunAsync("format", "--check", _root);

        _host.AssertExit(ExitCodes.Failed, exitCode);
        Assert.Contains("unreadable: Null.netpc.json:", _host.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckListsUnreadableAndNonCanonicalFiles()
    {
        Write("Bad.netpc.json", "{ not json");
        Write("Other.netpc.json", Compact());

        _host.AssertExit(ExitCodes.Failed, await _host.RunAsync("format", "--check", _root));

        Assert.Contains("unreadable: Bad.netpc.json:", _host.Output, StringComparison.Ordinal);
        Assert.Contains("not canonical: Other.netpc.json", _host.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DirectoriesAreSearchedRecursivelyAndBinAndObjAreSkipped()
    {
        string nested = Write(Path.Combine("a", "b", "N.netpc.json"), Compact());
        string skipped = Write(Path.Combine("obj", "Copy.netpc.json"), Compact());

        _host.AssertExit(ExitCodes.Success, await _host.RunAsync("format", _root));

        Assert.Equal(_canonical, File.ReadAllText(nested));
        Assert.Equal(Compact(), File.ReadAllText(skipped));
    }

    [Fact]
    public async Task NoArgumentFormatsTheCurrentDirectory()
    {
        string path = Write("A.netpc.json", Compact());

        _host.AssertExit(ExitCodes.Success, await _host.RunAsync("format"));

        Assert.Equal(_canonical, File.ReadAllText(path));
    }

    [Fact]
    public async Task AGraphFileArgumentIsFormattedDirectly()
    {
        string path = Write("A.netpc.json", Compact());
        string other = Write("Other.netpc.json", Compact());

        _host.AssertExit(ExitCodes.Success, await _host.RunAsync("format", path));

        Assert.Equal(_canonical, File.ReadAllText(path));
        Assert.Equal(Compact(), File.ReadAllText(other));
    }

    [Fact]
    public async Task AMissingPathExits2()
    {
        Assert.Equal(ExitCodes.Usage, await _host.RunAsync("format", Path.Combine(_root, "nope")));
        Assert.Contains("does not exist", _host.Error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ANonGraphFileExits2()
    {
        string file = Write("notes.txt", "x");

        Assert.Equal(ExitCodes.Usage, await _host.RunAsync("format", file));
    }

    [Fact]
    public async Task AnUnknownExtensionNodeSurvivesFormatting()
    {
        string graph = _canonical.Replace(
            "{ \"$kind\": \"return\", \"id\": \"n000000000vny2\" },",
            "{ \"$kind\": \"return\", \"id\": \"n000000000vny2\" },\n          { \"$kind\": \"netprints.test/Log\", \"id\": \"n000000000vny9\", \"level\": 3 },",
            StringComparison.Ordinal);
        string path = Write("A.netpc.json", graph);

        _host.AssertExit(ExitCodes.Success, await _host.RunAsync("format", _root));

        Assert.Contains("\"netprints.test/Log\"", File.ReadAllText(path), StringComparison.Ordinal);
        Assert.Contains("\"level\": 3", File.ReadAllText(path), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheCheckedInSamplesAreCanonical()
    {
        var host = new CliTestHost(LocalSdkLayout.FindRepositoryRoot());

        host.AssertExit(ExitCodes.Success, await host.RunAsync("format", "--check", "samples"));
    }
}
