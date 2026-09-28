using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Editor.Hosting;
using NetPrints.Extensibility.Loading;
using NetPrints.Projects;
using NetPrints.Serialization;
using NetPrints.Testing;
using NetPrints.Workspace;

namespace NetPrints.Editor.Tests.Hosting;

/// <summary>
/// SC-005: opening a real, already-restored copy of <c>samples/HelloWorld</c> through the same
/// stages the editor runs on startup — MSBuild evaluation and restore check
/// (<see cref="MsBuildProjectSystem.LoadAsync"/>), graph loading (<see cref="ProjectPersistence"/>),
/// extension loading (<see cref="ExtensionHost"/>) and reflection/type loading
/// (<see cref="ReflectionHost"/>). Timings go to the test output; the assertion is a generous
/// regression bound (3x the spec's target), same convention as <see cref="Search.SearchPerformanceTests"/>,
/// so a busy CI runner does not fail the build.
/// </summary>
/// <remarks>
/// Batch D4: the pipeline's first run in the process also pays <see cref="ReflectionHost"/>'s own
/// one-time Roslyn symbol-binding warm-up (its XML doc: "about 1.5 s" normally, unbounded under a
/// contended CI runner — this made the assertion flaky, e.g. 10.7 s in CI run 36381532778). That cost
/// is process-wide and paid once regardless of which project opens first; it is not part of what a
/// user experiences opening a *second* project in an already-running editor, which is what SC-005 is
/// about. So, like <see cref="Search.SearchPerformanceTests"/> (which measures its search step only,
/// never the cold reflection load it logs alongside it), the pipeline runs once untimed to pay that
/// cost, then once timed for the assertion.
/// </remarks>
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

        LocalSdkLayout.Write(directory);
        string csprojPath = Path.Combine(directory, "HelloWorld.csproj");

        // Warm-up: restores (obj/project.assets.json is missing on a fresh copy) and pays the
        // process's one-time Roslyn warm-up (see remarks). Not timed: SC-005 measures a steady-state
        // open, not the process's first one.
        await OpenOnceAsync(csprojPath, cancellationToken);

        (TimeSpan total, TimeSpan[] stageElapsed) = await OpenOnceAsync(csprojPath, cancellationToken);

        output.WriteLine($"SC-005 restored open: evaluation+restore-check {stageElapsed[0].TotalMilliseconds:F0} ms; " +
            $"+ graphs {stageElapsed[1].TotalMilliseconds:F0} ms; + extensions {stageElapsed[2].TotalMilliseconds:F0} ms; " +
            $"+ types {total.TotalMilliseconds:F0} ms total");

        Assert.True(total.TotalMilliseconds < OpenBoundMs, $"opening HelloWorld took {total.TotalMilliseconds:F0} ms");
    }

    private async Task<(TimeSpan Total, TimeSpan[] StageElapsed)> OpenOnceAsync(
        string csprojPath, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var stageElapsed = new TimeSpan[3];

        var projects = new MsBuildProjectSystem(new ProjectSystemOptions([], "9.9.9-test"), new ProcessRunner(), NullLogger<MsBuildProjectSystem>.Instance);
        ProjectSnapshot snapshot = await projects.LoadAsync(csprojPath, cancellationToken);
        stageElapsed[0] = stopwatch.Elapsed;

        ProjectPersistence persistence = TestEditor.CreatePersistence(projects);
        ProjectLoadResult loaded = await persistence.LoadAsync(snapshot, cancellationToken);
        Assert.Empty(loaded.Issues);
        stageElapsed[1] = stopwatch.Elapsed;

        await using ExtensionHost extensions = TestExtensions.CreateBuiltIn();
        await extensions.LoadForProjectAsync(snapshot.ExtensionFolders, cancellationToken);
        stageElapsed[2] = stopwatch.Elapsed;

        ReflectionHost reflection = TestEditor.CreateReflectionHost(extensions);
        await reflection.ReloadAsync(loaded.Project, cancellationToken);
        stopwatch.Stop();

        Assert.True(reflection.IsLoaded);
        return (stopwatch.Elapsed, stageElapsed);
    }
}
