using System.Globalization;
using System.Threading.Channels;

namespace NetPrints.Desktop.E2ETests.Hosting;

/// <summary>
/// A pool of desktop workers for the E2E run (batch D2): each worker owns a private Xvfb display
/// with openbox already running (<see cref="XServer"/>), started once and reused across tests, so
/// a test never pays for X server startup. The editor itself is started fresh on
/// <see cref="RentAsync"/> in the renting test's own working directory (a pre-started spare was
/// tried and reverted — GTK working-directory constraint, see docs/adr/0006-parallel-desktop-e2e.md).
/// No test shares a display or an editor, so none need a serial xUnit collection (owner-approved
/// audit; see the ADR). A worker is always returned — even when starting its editor fails, or
/// disposing one throws — so one flaky startup fails only its own caller, not every queued test
/// (R3-01).
/// </summary>
public sealed class DesktopWorkerPool : IAsyncLifetime
{
    /// <summary>Overrides the pool size (default <c>min(ProcessorCount / 2, 4)</c>, at least 1).</summary>
    public const string WorkersVariable = "NETPRINTS_E2E_WORKERS";

    /// <summary>
    /// Bounds a <see cref="RentAsync{T}"/> queue wait: well over the ~5-minute serial baseline for
    /// all seven scenarios, well under the CI job's 30-minute timeout, so a pool that never frees a
    /// worker fails its own queued callers with a clear <see cref="TimeoutException"/> and a TRX,
    /// instead of the whole job dying with neither (R3-01).
    /// </summary>
    private static readonly TimeSpan RentTimeout = TimeSpan.FromMinutes(10);

    private readonly List<Worker> workers = [];
    private readonly Channel<Worker> available = Channel.CreateUnbounded<Worker>();

    /// <summary>Whether E2E tests run in this environment; otherwise they are skipped (same gate as <see cref="XServer"/>).</summary>
    public static bool IsEnabled => XServer.IsEnabled;

    public static int PoolSize()
    {
        string? configured = Environment.GetEnvironmentVariable(WorkersVariable);
        if (int.TryParse(configured, NumberStyles.Integer, CultureInfo.InvariantCulture, out int requested) && requested > 0)
        {
            return requested;
        }

        return Math.Clamp(Environment.ProcessorCount / 2, 1, 4);
    }

    public async ValueTask InitializeAsync()
    {
        if (!IsEnabled)
        {
            return;
        }

        for (int i = 0; i < PoolSize(); i++)
        {
            var server = new XServer();
            var worker = new Worker(server);
            workers.Add(worker); // added before InitializeAsync, so DisposeAsync cleans up a partial start too (R3-04)
            await server.InitializeAsync();
            available.Writer.TryWrite(worker);
        }
    }

    /// <summary>
    /// Rents a worker's already-running display and starts a fresh editor on it, with
    /// <paramref name="workDirectory"/> as its working directory. Never the same editor process
    /// twice; never two editors on the same display at once.
    /// </summary>
    public Task<DesktopLease> RentAsync(CancellationToken cancellationToken, string workDirectory) =>
        RentAsync(cancellationToken, async (worker, token) =>
        {
            var editor = await EditorProcess.StartAsync(worker.Server, workDirectory, project: null, token);
            return new DesktopLease(this, worker, editor);
        });

    /// <summary>
    /// Rents a worker and hands it to <paramref name="start"/>, returning the worker to the pool if
    /// <paramref name="start"/> throws (R3-01), instead of leaking it. A test seam: a fault-injection
    /// test can fail "starting the editor" without a real display; <see cref="RentAsync(CancellationToken, string)"/>
    /// is the production path.
    /// </summary>
    internal async Task<T> RentAsync<T>(CancellationToken cancellationToken, Func<Worker, CancellationToken, Task<T>> start)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(RentTimeout);

        Worker worker;
        try
        {
            worker = await available.Reader.ReadAsync(deadline.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"No worker became available within {RentTimeout} (a leaked or exhausted pool).");
        }

        try
        {
            return await start(worker, cancellationToken);
        }
        catch
        {
            Return(worker);
            throw;
        }
    }

    internal void Return(Worker worker) => available.Writer.TryWrite(worker);

    public async ValueTask DisposeAsync()
    {
        foreach (var worker in workers)
        {
            await worker.Server.DisposeAsync();
        }
    }

    internal sealed class Worker(XServer server)
    {
        public XServer Server { get; } = server;
    }
}

/// <summary>
/// A rented worker: a display (<see cref="Server"/>) and a fresh <see cref="Editor"/>, never used
/// by another test. Disposing it disposes the editor and returns the worker (display) to the pool,
/// even if disposing the editor throws (R3-01).
/// </summary>
public sealed class DesktopLease : IAsyncDisposable
{
    private readonly DesktopWorkerPool pool;
    private readonly DesktopWorkerPool.Worker worker;
    private readonly EditorProcess editor;

    internal DesktopLease(DesktopWorkerPool pool, DesktopWorkerPool.Worker worker, EditorProcess editor)
    {
        this.pool = pool;
        this.worker = worker;
        this.editor = editor;
    }

    public XServer Server => worker.Server;

    public EditorProcess Editor => editor;

    public async ValueTask DisposeAsync()
    {
        try
        {
            await Editor.DisposeAsync();
        }
        finally
        {
            pool.Return(worker);
        }
    }
}
