namespace NetPrints.Desktop.E2ETests.Hosting;

/// <summary>
/// Fault-injection coverage for <see cref="DesktopWorkerPool"/> (R3-01), run unconditionally: seeds
/// the pool with a bare worker via <see cref="DesktopWorkerPool.Return"/> and swaps in a fake
/// "start the editor" step via the internal <see cref="DesktopWorkerPool.RentAsync{T}"/> test seam,
/// so no real Xvfb/openbox/editor process is needed.
/// </summary>
public sealed class DesktopWorkerPoolTests
{
    [Fact]
    public async Task RentAsyncReturnsTheWorkerWhenStartingFails()
    {
        var pool = new DesktopWorkerPool();
        var worker = new DesktopWorkerPool.Worker(new XServer());
        pool.Return(worker);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => pool.RentAsync<int>(CancellationToken.None, (_, _) => throw new InvalidOperationException("the editor did not start")));

        // The next rent must succeed promptly: if the failed worker had not been returned, this
        // would hang until the pool's own (much longer) queue-wait deadline instead.
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        DesktopWorkerPool.Worker rented = await pool.RentAsync(deadline.Token, (w, _) => Task.FromResult(w));

        Assert.Same(worker, rented);
    }
}
