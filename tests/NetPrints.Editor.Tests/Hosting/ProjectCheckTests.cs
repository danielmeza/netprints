using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Core;
using NetPrints.Desktop;
using NetPrints.Editor.Hosting;
using NetPrints.Projects;
using NetPrints.Testing;
using NetPrints.Workspace;

namespace NetPrints.Editor.Tests.Hosting;

/// <summary>
/// RL-T06/release contract §5: <see cref="ProjectCheck"/>'s exit codes for bad arguments, a project
/// error and no registered SDK. The success path (0) and <c>--run</c> (0/4) are covered end to end by
/// <c>scripts/smoke-desktop.sh</c> instead, which needs a self-contained publish.
/// </summary>
public sealed class ProjectCheckTests : IDisposable
{
    private readonly List<string> cleanup = [];

    public void Dispose() => cleanup.ForEach(TestPaths.TryDelete);

    private string CopyHelloWorldWithLocalSdk()
    {
        string csprojPath = TestPaths.CopyHelloWorldSample();
        cleanup.Add(csprojPath);
        string directory = Path.GetDirectoryName(csprojPath) is { } dir ? dir : throw new InvalidOperationException("No directory.");
        LocalSdkLayout.Write(directory);
        return csprojPath;
    }

    /// <summary>Renames the sample's <c>Console.WriteLine</c> call to a method that does not exist, so analysis reports one real Roslyn error.</summary>
    private static void BreakMethodReference(string csprojPath)
    {
        string graphPath = Path.Combine(Path.GetDirectoryName(csprojPath) ?? "", "HelloWorld.Program.netpc.json");
        JsonNode root = JsonNode.Parse(File.ReadAllText(graphPath)) ?? throw new InvalidOperationException("Empty graph document.");
        JsonNode callNode = root["methods"]?[0]?["graph"]?["nodes"]?[2] ?? throw new InvalidOperationException("Expected node not found.");
        JsonNode method = callNode["method"] ?? throw new InvalidOperationException("Expected node has no method.");
        method["name"] = "DoesNotExist";
        File.WriteAllText(graphPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    [Fact(Timeout = 120_000)]
    public async Task CheckProjectWithBrokenMethodReferenceReturnsAnalysisError()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        string csprojPath = CopyHelloWorldWithLocalSdk();
        BreakMethodReference(csprojPath);

        var output = new StringWriter();
        int exitCode = await ProjectCheck.RunAsync(csprojPath, run: false, output, cancellationToken);

        Assert.Equal(1, exitCode);
        string text = output.ToString();
        Assert.Contains("analysis: 1 errors, 0 warnings", text, StringComparison.Ordinal);
        Assert.Contains("error CS0117", text, StringComparison.Ordinal);
        Assert.Contains("(graph ", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckProjectWithNoPathReturnsBadArguments()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var output = new StringWriter();

        int exitCode = await ProjectCheck.RunAsync(null, run: false, output, cancellationToken);

        Assert.Equal(2, exitCode);
    }

    [Fact]
    public async Task CheckProjectWithDashDashArgumentReturnsBadArguments()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var output = new StringWriter();

        // "--check-project --run" (no path) reaches RunAsync with "--run" in projectPath's place.
        int exitCode = await ProjectCheck.RunAsync("--run", run: false, output,
            msBuildAvailable: true, registeredInstance: null, new NoSdkProjectSystem(), new ProcessRunner(),
            NullLoggerFactory.Instance, cancellationToken);

        Assert.Equal(2, exitCode);
    }

    [Fact]
    public async Task CheckProjectWithRelativePathIsResolvedToFullPathBeforeLoading()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var output = new StringWriter();
        var projects = new FakeProjectSystem();

        // A bare relative file name used to reach MsBuildProjectSystem's GetDirectoryOrThrow with no
        // directory component and throw ArgumentException (R1-14); seed the snapshot at the resolved
        // full path so a fixed RunAsync loads that path instead of the untouched relative one.
        string relativePath = "netprints-check-relative-path.csproj";
        string fullPath = Path.GetFullPath(relativePath);
        projects.Seed(new ProjectSnapshot(fullPath, "Test", "Test", "Test", BinaryType.SharedLibrary,
            "net10.0", DefaultProjectProfile.ProfileId, true, [], [], [], [], [], "{}",
            new Dictionary<string, string>(), []));

        int exitCode = await ProjectCheck.RunAsync(relativePath, run: false, output,
            msBuildAvailable: true, registeredInstance: null, projects, new ProcessRunner(),
            NullLoggerFactory.Instance, cancellationToken);

        Assert.Equal(fullPath, Assert.Single(projects.LoadCalls));
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task CheckProjectWithNoSdkReturnsNoSdkExitCode()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var output = new StringWriter();

        // Fakes "no .NET SDK" without touching the real, process-wide MSBuildLocator state that
        // MsBuildTestInitializer and every other MSBuild-dependent test in this assembly rely on.
        int exitCode = await ProjectCheck.RunAsync("unused.csproj", run: false, output,
            msBuildAvailable: false, registeredInstance: null, new NoSdkProjectSystem(), new ProcessRunner(),
            NullLoggerFactory.Instance, cancellationToken);

        Assert.Equal(3, exitCode);
        Assert.Contains(ProjectSystemException.NoSdkRegistered, output.ToString(), StringComparison.Ordinal);
    }
}
