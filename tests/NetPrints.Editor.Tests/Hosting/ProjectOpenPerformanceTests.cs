using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Editor.Hosting;
using NetPrints.Extensibility.Loading;
using NetPrints.Projects;
using NetPrints.Serialization;
using NetPrints.Workspace;

namespace NetPrints.Editor.Tests.Hosting;

/// <summary>
/// SC-005: opening a real, already-restored copy of <c>samples/HelloWorld</c> through the same
/// stages the editor runs on startup — MSBuild evaluation and restore check
/// (<see cref="MsBuildProjectSystem.LoadAsync"/>), graph loading (<see cref="ProjectPersistence"/>),
/// extension loading (<see cref="ExtensionHost"/>) and reflection/type loading
/// (<see cref="ReflectionHost"/>, including its warm-up). Timings go to the test output; the
/// assertion is a generous regression bound (3x the spec's target), same convention as
/// <see cref="Search.SearchPerformanceTests"/>, so a busy CI runner does not fail the build.
/// </summary>
public sealed class ProjectOpenPerformanceTests : IDisposable
{
    // SC-005 target: at most 3 s once restored. Asserted with a 3x margin.
    private const double OpenBoundMs = 9000;

    private readonly string directory = TestPaths.CreateTempDirectory();

    public void Dispose() => TestPaths.TryDelete(directory);

    [Fact(Timeout = 180_000)]
    [Trait("Category", "Performance")]
    public async Task OpenHelloWorldRestoredIsWithinBudget()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var output = TestContext.Current.TestOutputHelper;
        Assert.NotNull(output);

        string source = Path.Combine(AppContext.BaseDirectory, "samples", "HelloWorld");
        foreach (string file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(directory, Path.GetFileName(file)));
        }

        TestPaths.WriteLocalSdkLayout(directory);
        string csprojPath = Path.Combine(directory, "HelloWorld.csproj");

        // Warm-up: the first LoadAsync on a fresh copy restores (obj/project.assets.json is missing).
        // Not timed: SC-005 measures an already-restored open.
        await new MsBuildProjectSystem(new ProjectSystemOptions([], "9.9.9-test"), new ProcessRunner(), NullLogger<MsBuildProjectSystem>.Instance)
            .LoadAsync(csprojPath, cancellationToken);

        var stopwatch = Stopwatch.StartNew();

        var projects = new MsBuildProjectSystem(new ProjectSystemOptions([], "9.9.9-test"), new ProcessRunner(), NullLogger<MsBuildProjectSystem>.Instance);
        ProjectSnapshot snapshot = await projects.LoadAsync(csprojPath, cancellationToken);
        var evaluationElapsed = stopwatch.Elapsed;

        ProjectPersistence persistence = TestEditor.CreatePersistence(projects);
        ProjectLoadResult loaded = await persistence.LoadAsync(snapshot, cancellationToken);
        Assert.Empty(loaded.Issues);
        var graphsElapsed = stopwatch.Elapsed;

        await using ExtensionHost extensions = TestExtensions.CreateBuiltIn();
        await extensions.LoadForProjectAsync(snapshot.ExtensionFolders, cancellationToken);
        var extensionsElapsed = stopwatch.Elapsed;

        ReflectionHost reflection = TestEditor.CreateReflectionHost(extensions);
        await reflection.ReloadAsync(loaded.Project, cancellationToken);
        stopwatch.Stop();

        output.WriteLine($"SC-005 restored open: evaluation+restore-check {evaluationElapsed.TotalMilliseconds:F0} ms; " +
            $"+ graphs {graphsElapsed.TotalMilliseconds:F0} ms; + extensions {extensionsElapsed.TotalMilliseconds:F0} ms; " +
            $"+ types (incl. warm-up) {stopwatch.Elapsed.TotalMilliseconds:F0} ms total");

        Assert.True(reflection.IsLoaded);
        Assert.True(stopwatch.ElapsedMilliseconds < OpenBoundMs, $"opening HelloWorld took {stopwatch.ElapsedMilliseconds} ms");
    }
}
