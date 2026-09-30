using System;
using System.IO;
using System.Threading.Tasks;
using NetPrints.Cli.Tests.Support;
using NetPrints.Testing;
using Xunit;

namespace NetPrints.Cli.Tests.Commands;

/// <summary>CL-T09: <c>migrate</c> reports each graph's schema version and writes nothing while version 1 is current.</summary>
public sealed class MigrateCommandTests : IDisposable
{
    private const string V1Graph = """{ "schemaVersion": 1, "namespace": "N", "name": "C", "classGraph": { "nodes": [] } }""";
    private const string V2Graph = """{ "schemaVersion": 2, "namespace": "N", "name": "D", "classGraph": { "nodes": [] } }""";

    private readonly string _root = Directory.CreateTempSubdirectory("np-migrate-").FullName;
    private readonly CliTestHost _host;

    public MigrateCommandTests() => _host = new CliTestHost(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Write(string relativePath, string content)
    {
        string path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? _root);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public async Task AllVersion1GraphsReportCurrentAndExit0WithoutWriting()
    {
        string a = Write("A.netpc.json", V1Graph);
        string b = Write("sub/B.netpc.json", V1Graph);
        DateTime before = File.GetLastWriteTimeUtc(a);

        int exitCode = await _host.RunAsync("migrate", _root);

        Assert.Equal(ExitCodes.Success, exitCode);
        string[] lines = _host.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.Contains("A.netpc.json: schema 1 (current)", lines);
        Assert.Contains($"sub{Path.DirectorySeparatorChar}B.netpc.json: schema 1 (current)", lines);
        Assert.Equal("No migrations are available; 2 graph(s) are at schema version 1.", lines[^1]);
        Assert.Equal(before, File.GetLastWriteTimeUtc(a));
        Assert.Equal(V1Graph, File.ReadAllText(b));
        Assert.Empty(_host.Projects.LoadedProjects);
    }

    [Fact]
    public async Task ANewerSchemaVersionExits1NamingBothVersions()
    {
        Write("A.netpc.json", V1Graph);
        Write("D.netpc.json", V2Graph);

        int exitCode = await _host.RunAsync("migrate", _root);

        Assert.Equal(ExitCodes.Failed, exitCode);
        Assert.Contains("D.netpc.json: schema 2 is not supported (this tool supports 1)", _host.Output, StringComparison.Ordinal);
        Assert.Contains("A.netpc.json: schema 1 (current)", _host.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnreadableGraphExits1()
    {
        Write("Bad.netpc.json", "{ not json");

        Assert.Equal(ExitCodes.Failed, await _host.RunAsync("migrate", _root));
        Assert.Contains("Bad.netpc.json: unreadable:", _host.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AGraphFileArgumentIsReadDirectly()
    {
        string file = Write("A.netpc.json", V1Graph);
        Write("Other.netpc.json", V2Graph);

        Assert.Equal(ExitCodes.Success, await _host.RunAsync("migrate", file));
    }

    [Fact]
    public async Task ABinAndObjDirectoryIsNotSearched()
    {
        Write("A.netpc.json", V1Graph);
        Write("obj/Copy.netpc.json", V2Graph);

        Assert.Equal(ExitCodes.Success, await _host.RunAsync("migrate", _root));
    }

    [Fact]
    public async Task AProjectArgumentMigratesItsGraphsThroughTheProjectSystem()
    {
        string graph = Write("A.netpc.json", V1Graph);
        string project = Write("P.csproj", "<Project />");
        _host.Projects.GraphFiles = [graph];

        Assert.Equal(ExitCodes.Success, await _host.RunAsync("migrate", project));

        Assert.Equal([project], _host.Projects.LoadedProjects);
        Assert.Contains("A.netpc.json: schema 1 (current)", _host.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AProjectsExtensionNodesAreReadThroughItsExtensionFolders()
    {
        const string extensionGraph = """{ "schemaVersion": 1, "namespace": "N", "name": "E", "classGraph": { "nodes": [ { "$kind": "netprints.test/Log", "id": "n1" } ] } }""";
        string graph = Write("E.netpc.json", extensionGraph);
        string project = Write("P.csproj", "<Project />");
        _host.Projects.GraphFiles = [graph];
        _host.Projects.ExtensionFolders = [Path.Combine(LocalSdkLayout.FindRepositoryRoot(), "tests", "NetPrints.TestExtension", "bin", LocalSdkLayout.DetectConfiguration(), "extensions", "netprints.test")];

        int exitCode = await _host.RunAsync("migrate", project);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("E.netpc.json: schema 1 (current)", _host.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AProjectsBrokenExtensionFolderExits1WithItsDiagnostic()
    {
        string graph = Write("A.netpc.json", V1Graph);
        string project = Write("P.csproj", "<Project />");
        string broken = Directory.CreateDirectory(Path.Combine(_root, "broken-ext")).FullName;
        File.WriteAllText(Path.Combine(broken, "netprints-extension.json"), "{ not json");
        _host.Projects.GraphFiles = [graph];
        _host.Projects.ExtensionFolders = [broken];

        Assert.Equal(ExitCodes.Failed, await _host.RunAsync("migrate", project));
        Assert.Contains("NPX", _host.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NoArgumentUsesTheCurrentDirectorysProject()
    {
        string graph = Write("A.netpc.json", V1Graph);
        string project = Write("P.csproj", "<Project />");
        _host.Projects.GraphFiles = [graph];

        Assert.Equal(ExitCodes.Success, await _host.RunAsync("migrate"));

        Assert.Equal([project], _host.Projects.LoadedProjects);
    }

    [Fact]
    public async Task AMissingPathExits2()
    {
        Assert.Equal(ExitCodes.Usage, await _host.RunAsync("migrate", Path.Combine(_root, "nope")));
        Assert.Contains("does not exist", _host.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ANonGraphFileExits2()
    {
        string file = Write("notes.txt", "x");

        Assert.Equal(ExitCodes.Usage, await _host.RunAsync("migrate", file));
    }

    [Fact]
    public async Task ProjectArgumentWithoutSdkExits3()
    {
        string project = Write("P.csproj", "<Project />");
        _host.MsBuild.Available = false;

        Assert.Equal(ExitCodes.NoSdk, await _host.RunAsync("migrate", project));
    }
}
