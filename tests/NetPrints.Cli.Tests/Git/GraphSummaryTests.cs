using System;
using System.IO;
using System.Threading.Tasks;
using NetPrints.Cli.Tests.Support;
using NetPrints.Testing;
using Xunit;

namespace NetPrints.Cli.Tests.Git;

/// <summary>GI-T02: the <c>show</c> summary is stable, independent of node order, and tolerant of unloaded extension nodes.</summary>
public sealed class GraphSummaryTests : IDisposable
{
    private static readonly string Root = LocalSdkLayout.FindRepositoryRoot();
    private readonly string _temp = Directory.CreateTempSubdirectory("np-show-").FullName;

    public void Dispose() => Directory.Delete(_temp, recursive: true);

    private static string Fixture(string fixture, string file) => Path.Combine(Root, "tests", "NetPrints.Core.Tests", "Fixtures", fixture, file);

    private static string Golden(string name) => File.ReadAllText(Path.Combine(Root, "tests", "NetPrints.Cli.Tests", "Git", "Snapshots", name));

    private static string Normalize(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    private async Task<(int Exit, string Output, string Error)> ShowAsync(string path, bool textconv = false)
    {
        var host = new CliTestHost(_temp);
        string[] args = textconv ? ["show", "--textconv", path] : ["show", path];
        int exit = await host.RunAsync(args);
        return (exit, Normalize(host.Output), host.Error.ToString());
    }

    [Theory]
    [InlineData("HelloWorld", "HelloWorld.Program.netpc.json", "HelloWorld.show.txt")]
    [InlineData("AllNodes", "AllNodes.Everything.netpc.json", "AllNodes.show.txt")]
    public async Task ShowMatchesTheGolden(string fixture, string file, string golden)
    {
        (int exit, string output, string error) = await ShowAsync(Fixture(fixture, file));

        Assert.True(exit == ExitCodes.Success, error);
        Assert.Equal(Normalize(Golden(golden)), output);
    }

    [Fact]
    public async Task AnUnknownExtensionNodePrintsItsKindAndThatTheExtensionIsNotLoaded()
    {
        string graph = File.ReadAllText(Fixture("HelloWorld", "HelloWorld.Program.netpc.json")).Replace(
            "{ \"$kind\": \"return\", \"id\": \"n000000000vny2\" },",
            "{ \"$kind\": \"return\", \"id\": \"n000000000vny2\" },\n          { \"$kind\": \"netprints.test/Log\", \"id\": \"n000000000vny9\" },",
            StringComparison.Ordinal);
        string path = Path.Combine(_temp, "U.netpc.json");
        File.WriteAllText(path, graph);

        (int exit, string output, _) = await ShowAsync(path);

        Assert.Equal(ExitCodes.Success, exit);
        Assert.Contains("    node n000000000vny9 netprints.test/Log (extension not loaded)\n", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheOrderOfNodesInTheFileDoesNotChangeTheSummary()
    {
        string original = File.ReadAllText(Fixture("HelloWorld", "HelloWorld.Program.netpc.json"));
        string entry = """{ "$kind": "methodEntry", "id": "n000000000vny1" },""";
        string ret = """{ "$kind": "return", "id": "n000000000vny2" },""";
        Assert.Contains(entry + "\n          " + ret, original, StringComparison.Ordinal);
        string swapped = original.Replace(entry + "\n          " + ret, ret + "\n          " + entry, StringComparison.Ordinal);
        string path = Path.Combine(_temp, "S.netpc.json");
        File.WriteAllText(path, swapped);

        (int exit, string output, _) = await ShowAsync(path);

        Assert.Equal(ExitCodes.Success, exit);
        Assert.Equal(Normalize(Golden("HelloWorld.show.txt")), output);
    }

    [Fact]
    public async Task AnUnreadableFileExits1NamingItOnStderr()
    {
        string path = Path.Combine(_temp, "Bad.netpc.json");
        File.WriteAllText(path, "{ not json");

        (int exit, string output, string error) = await ShowAsync(path);

        Assert.Equal(ExitCodes.Failed, exit);
        Assert.Empty(output);
        Assert.Contains("Bad.netpc.json", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AGraphWithANullNodeListExits1AsUnreadableNotAnInternalError()
    {
        string path = Path.Combine(_temp, "Null.netpc.json");
        File.Copy(Path.Combine(Root, "tests", "NetPrints.Cli.Tests", "Git", "Fixtures", "NullNodes", "ours.conflicted.txt"), path);

        (int exit, _, string error) = await ShowAsync(path);

        Assert.Equal(ExitCodes.Failed, exit);
        Assert.Contains("unreadable", error, StringComparison.Ordinal);
        Assert.DoesNotContain("Internal error", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AMissingFileExits2()
    {
        (int exit, _, string error) = await ShowAsync(Path.Combine(_temp, "nope.netpc.json"));

        Assert.Equal(ExitCodes.Usage, exit);
        Assert.Contains("does not exist", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheHandWrittenGraphMatchesTheContractGrammar()
    {
        string fixtures = Path.Combine(Root, "tests", "NetPrints.Cli.Tests", "Git", "Fixtures", "ShowGrammar");

        (int exit, string output, string error) = await ShowAsync(Path.Combine(fixtures, "graph.txt"));

        Assert.True(exit == ExitCodes.Success, error);
        Assert.Equal(Normalize(File.ReadAllText(Path.Combine(fixtures, "expected.show.txt"))), output);
    }

    [Theory]
    [InlineData("Unreadable", "ours.conflicted.txt")]
    [InlineData("NullNodes", "ours.conflicted.txt")]
    public async Task TextconvPrintsAnUnreadableFileAsItsRawTextAndExits0(string fixture, string file)
    {
        string path = Path.Combine(Root, "tests", "NetPrints.Cli.Tests", "Git", "Fixtures", fixture, file);

        (int exit, string output, string error) = await ShowAsync(path, textconv: true);

        Assert.True(exit == ExitCodes.Success, error);
        Assert.Equal(Normalize(File.ReadAllText(path)), output);
    }

    [Fact]
    public async Task TextconvPrintsAGraphOfANewerSchemaAsItsRawTextAndExits0()
    {
        string path = Path.Combine(_temp, "Newer.netpc.json");
        File.WriteAllText(path, File.ReadAllText(Fixture("HelloWorld", "HelloWorld.Program.netpc.json")).Replace("\"schemaVersion\": 1", "\"schemaVersion\": 99", StringComparison.Ordinal));

        (int exit, string output, string error) = await ShowAsync(path, textconv: true);

        Assert.True(exit == ExitCodes.Success, error);
        Assert.Equal(Normalize(File.ReadAllText(path)), output);
    }

    [Fact]
    public async Task TextconvOfAReadableGraphPrintsTheSummary()
    {
        (int exit, string output, string error) = await ShowAsync(Fixture("HelloWorld", "HelloWorld.Program.netpc.json"), textconv: true);

        Assert.True(exit == ExitCodes.Success, error);
        Assert.Equal(Normalize(Golden("HelloWorld.show.txt")), output);
    }

    [Fact]
    public async Task TextconvOfAMissingFileStillExits2()
    {
        (int exit, _, _) = await ShowAsync(Path.Combine(_temp, "nope.netpc.json"), textconv: true);

        Assert.Equal(ExitCodes.Usage, exit);
    }
}
