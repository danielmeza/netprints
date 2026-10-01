using NetPrints.Desktop.E2ETests.Driving;
using NetPrints.Desktop.E2ETests.Hosting;
using NetPrints.Editor.Hosting;
using NetPrints.Testing;
using NetPrints.Testing.Ui.Scenarios;
using NetPrints.Testing.Ui.Screenplay;
using Xunit.Sdk;

namespace NetPrints.Desktop.E2ETests.Scenarios;

/// <summary>
/// The shared smoke flows on the real desktop editor (X11, xdotool, GTK pickers). One sealed class
/// per scenario below, each with a single <c>[Fact]</c>: xUnit's unit of parallelism is the test
/// collection, and every test method in one class shares that class's (default) collection and so
/// runs serially within it — six facts in one class would stay serial no matter how the worker pool
/// or xunit.runner.json are configured. Splitting them lets xUnit schedule all six onto the pool at
/// once; see docs/adr/0006-parallel-desktop-e2e.md for why none of them need a serial collection.
/// </summary>
public abstract class X11SmokeTestBase(DesktopWorkerPool pool) : SmokeScenarios, IAsyncDisposable
{
    /// <summary>
    /// The test's own work budget: 180 s starting once it has rented a worker, not at dispatch
    /// (batch D3). All seven scenarios dispatch at once (<c>maxParallelThreads: 8</c>) onto a pool
    /// of far fewer workers, so most of them queue for a while first; counting that queue wait
    /// against a fixed per-test <c>[Fact(Timeout = ...)]</c> (which starts the clock at dispatch)
    /// let a busy run fail a scenario that never got a slow step of its own. See
    /// docs/adr/0006-parallel-desktop-e2e.md.
    /// </summary>
    protected const int Timeout = 180_000;

    private readonly string work = Directory.CreateTempSubdirectory("netprints-e2e-").FullName;
    private readonly CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private DesktopLease? lease;
    private X11Driver? driver;

    private StepTimer? timer;
    private bool scenarioEnded;
    private volatile bool editorExited;

    /// <summary>The step to hold until the test's budget runs out, to prove the diagnostics (<see cref="E2EDiagnosticsTests"/>); <see langword="null"/> in every other test.</summary>
    protected virtual string? ForcedTimeoutStep => null;

    /// <summary>The test's own work budget, counted from the rented worker.</summary>
    protected virtual TimeSpan Budget => TimeSpan.FromMilliseconds(Timeout);

    protected CancellationToken Token => timeoutCts.Token;

    private StepTimer Steps => timer ??= new StepTimer(TestContext.Current.TestMethod?.MethodName ?? "test", ForcedTimeoutStep);

    private static string Artifacts => Path.Combine(
        Environment.GetEnvironmentVariable("NETPRINTS_UI_ARTIFACTS") is { Length: > 0 } configured ? configured : Path.Combine(AppContext.BaseDirectory, "ui-artifacts"),
        "e2e", TestContext.Current.TestMethod?.MethodName ?? "test");

    protected override IDisposable Step(string name) => Steps.Step(name);

    /// <summary>
    /// Runs a scenario; when it fails (an exception, an assertion, the test's budget running out or the
    /// editor exiting), captures the diagnostics (contracts/ci.md §3) and throws an
    /// <see cref="E2EStepFailureException"/> that keeps the original failure as its inner exception.
    /// </summary>
    protected async Task RunScenarioAsync(Func<CancellationToken, Task> scenario)
    {
        using var watch = Steps.WatchFailures();
        try
        {
            await scenario(Token);
        }
        catch (Exception original) when (lease is not null && driver is not null)
        {
            var moment = Steps.WhereFailed(original);
            string kind = editorExited ? "editor exited" : timeoutCts.IsCancellationRequested ? "timeout" : original is Xunit.Sdk.XunitException ? "assertion" : "exception";
            var parts = DiagnosticParts.Create(kind, GetType().Name, Steps, moment, lease, driver.Tool);
            throw await new FailureCapture(TimeProvider.System, parts)
                .FailAsync(original, moment.Step, moment.Elapsed, FailureCapture.FolderFor(GetType().Name), CancellationToken.None);
        }
        finally
        {
            scenarioEnded = true;
        }
    }

    protected override async Task<SmokeContext> StartAsync(CancellationToken cancellationToken)
    {
        if (!DesktopWorkerPool.IsEnabled)
        {
            Assert.Skip($"Desktop E2E tests run with {XServer.EnableVariable}=1 (Linux with Xvfb, openbox, xdotool, ImageMagick and GTK 3).");
        }

        // Arrange: a private copy of the checked-in sample, with a local-SDK layout so it builds
        // against this repository's own generator (T059/T062a, research.md R21). The editor starts
        // without a project.
        string sample = Directory.CreateDirectory(Path.Combine(work, "HelloWorld")).FullName;
        foreach (string file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "samples", "HelloWorld")))
        {
            File.Copy(file, Path.Combine(sample, Path.GetFileName(file)));
        }

        LocalSdkLayout.Write(sample);

        lease = await pool.RentAsync(cancellationToken, work);
        timeoutCts.CancelAfter(Budget); // the budget starts now, not at dispatch (batch D3)
        lease.Editor.Exited.ContinueWith(_ =>
        {
            if (!scenarioEnded)
            {
                editorExited = true;
                timeoutCts.Cancel();
            }
        }, TaskScheduler.Default).Forget(_ => { });
        driver = new X11Driver(lease.Server, lease.Editor, new Tool(lease.Server));
        var actor = Actor.Named("Ada").WhoCan(UseNetPrints.With(driver, new GtkFileDialogs(driver, lease.Editor)));
        await actor.Using<UseNetPrints>().MainWindow.GetAsync(cancellationToken);
        await CheckpointAsync(new SmokeContext(actor, "", work), "00-started", cancellationToken);
        return new SmokeContext(actor, Path.Combine(sample, "HelloWorld.csproj"), Directory.CreateDirectory(Path.Combine(work, "out")).FullName);
    }

    protected override async Task CheckpointAsync(SmokeContext context, string name, CancellationToken cancellationToken)
    {
        await Steps.HoldIfForcedAsync(cancellationToken);
        var screen = await (driver ?? throw new InvalidOperationException("The editor has not started.")).ScreenAsync(cancellationToken);
        screen.Save(Path.Combine(Artifacts, name + ".png"));
    }

    /// <summary>Diagnostics for every test (the last state of a failing one): screen, UI dump, logs, xdotool calls.</summary>
    public async ValueTask DisposeAsync()
    {
        if (lease is not null && driver is not null)
        {
            try
            {
                try
                {
                    Directory.CreateDirectory(Artifacts);
                    (await driver.ScreenAsync(CancellationToken.None)).Save(Path.Combine(Artifacts, "zz-final.png"));
                    await File.WriteAllTextAsync(Path.Combine(Artifacts, "tree.txt"), await driver.DumpAsync(CancellationToken.None));
                }
                catch (Exception e) when (e is IOException or InvalidOperationException or TimeoutException)
                {
                    // Best effort: the editor may have crashed (see its log).
                }

                await File.WriteAllTextAsync(Path.Combine(Artifacts, "editor-stdout.txt"), lease.Editor.Output);
                await File.WriteAllTextAsync(Path.Combine(Artifacts, "editor-stderr.txt"), lease.Editor.Errors);
                await File.WriteAllTextAsync(Path.Combine(Artifacts, "xdotool.txt"), driver.Tool.Log);
            }
            finally
            {
                // Disposing the lease (and so returning its worker) must not depend on the artifact
                // writes above succeeding, or an IO failure there leaks the editor and the worker (R3-01).
                await lease.DisposeAsync();
            }
        }

        try
        {
            Directory.Delete(work, true);
        }
        catch (IOException)
        {
            // Best effort.
        }

        timeoutCts.Dispose();
    }
}

public sealed class EditCompileAndRunTests(DesktopWorkerPool pool) : X11SmokeTestBase(pool)
{
    [Fact]
    public Task EditCompileAndRun() => RunScenarioAsync(EditCompileAndRunAsync);
}

public sealed class CreateProjectTests(DesktopWorkerPool pool) : X11SmokeTestBase(pool)
{
    [Fact]
    public Task CreateProject() => RunScenarioAsync(CreateProjectAsync);
}

public sealed class AddReferencesTests(DesktopWorkerPool pool) : X11SmokeTestBase(pool)
{
    [Fact]
    public Task AddReferences() => RunScenarioAsync(token => AddReferencesAsync(typeof(object).Assembly.Location, token));
}

public sealed class MinimizeAndRestoreClassWindowTests(DesktopWorkerPool pool) : X11SmokeTestBase(pool)
{
    [Fact]
    public Task MinimizeAndRestoreClassWindow() => RunScenarioAsync(MinimizeAndRestoreClassWindowAsync);
}

public sealed class PanCursorTests(DesktopWorkerPool pool) : X11SmokeTestBase(pool)
{
    [Fact]
    public Task PanCursor() => RunScenarioAsync(PanCursorAsync);
}

public sealed class DragFromListsTests(DesktopWorkerPool pool) : X11SmokeTestBase(pool)
{
    [Fact]
    public Task DragFromLists() => RunScenarioAsync(DragFromListsAsync);
}
