using Avalonia.Controls.ApplicationLifetimes;
using NetPrints.Editor.Hosting;

namespace NetPrints.Editor.Tests.Hosting;

/// <summary>
/// <see cref="ShutdownCoordinator"/>: coalesces repeated shutdown requests into a single cleanup
/// pass, then lets shutdown proceed even if the cleanup threw.
/// </summary>
public class ShutdownCoordinatorTests
{
    [Fact]
    public void TwoRequestsWhileCleanupIsPendingInvokeCleanupOnceAndCancelBoth()
    {
        var cleanupGate = new TaskCompletionSource();
        int cleanupCalls = 0;
        var coordinator = new ShutdownCoordinator(
            async () =>
            {
                Interlocked.Increment(ref cleanupCalls);
                await cleanupGate.Task;
            },
            () => { },
            new CollectingLogger<ShutdownCoordinator>());

        var first = new ShutdownRequestedEventArgs();
        var second = new ShutdownRequestedEventArgs();
        coordinator.OnShutdownRequested(first);
        coordinator.OnShutdownRequested(second);

        Assert.True(first.Cancel);
        Assert.True(second.Cancel);
        Assert.Equal(1, cleanupCalls);

        // Let the pending cleanup finish so it does not outlive the test.
        cleanupGate.SetResult();
    }

    [Fact]
    public async Task ShutdownIsCalledOnceAfterCleanupCompletes()
    {
        var cleanupGate = new TaskCompletionSource();
        var shutdownCalled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int shutdownCalls = 0;
        var coordinator = new ShutdownCoordinator(
            () => new ValueTask(cleanupGate.Task),
            () =>
            {
                Interlocked.Increment(ref shutdownCalls);
                shutdownCalled.TrySetResult();
            },
            new CollectingLogger<ShutdownCoordinator>());

        coordinator.OnShutdownRequested(new ShutdownRequestedEventArgs());
        Assert.Equal(0, Volatile.Read(ref shutdownCalls));

        cleanupGate.SetResult();
        await shutdownCalled.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.Equal(1, Volatile.Read(ref shutdownCalls));
    }

    [Fact]
    public async Task ARequestAfterCleanupHasCompletedIsNotCancelled()
    {
        var shutdownCalled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var coordinator = new ShutdownCoordinator(
            () => ValueTask.CompletedTask,
            () => shutdownCalled.TrySetResult(),
            new CollectingLogger<ShutdownCoordinator>());

        coordinator.OnShutdownRequested(new ShutdownRequestedEventArgs());
        await shutdownCalled.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        var afterCompletion = new ShutdownRequestedEventArgs();
        coordinator.OnShutdownRequested(afterCompletion);

        Assert.False(afterCompletion.Cancel);
    }

    [Fact]
    public void AThrowingCleanupIsLoggedAndShutdownStillRuns()
    {
        var failure = new InvalidOperationException("cleanup boom");
        int shutdownCalls = 0;
        var logger = new CollectingLogger<ShutdownCoordinator>();
        var coordinator = new ShutdownCoordinator(
            () => throw failure,
            () => shutdownCalls++,
            logger);

        coordinator.OnShutdownRequested(new ShutdownRequestedEventArgs());

        Assert.Equal(1, shutdownCalls);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(1024, entry.EventId.Id);
        Assert.Same(failure, entry.Exception);
    }

    [Fact]
    public void AThrowingShutdownIsLoggedInsteadOfFaultingTheUnobservedCleanupTask()
    {
        // R2-27: shutdown() used to run in the cleanup try's finally, so a throw there faulted the
        // task OnShutdownRequested starts and nobody awaits (an unobserved task exception).
        var failure = new InvalidOperationException("shutdown boom");
        var logger = new CollectingLogger<ShutdownCoordinator>();
        var coordinator = new ShutdownCoordinator(
            () => ValueTask.CompletedTask,
            () => throw failure,
            logger);

        var exception = Record.Exception(() => coordinator.OnShutdownRequested(new ShutdownRequestedEventArgs()));

        Assert.Null(exception);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(1024, entry.EventId.Id);
        Assert.Same(failure, entry.Exception);
    }

    [Fact]
    public async Task ADeclinedConfirmationCancelsTheRequestSkipsCleanupAndTheNextRequestAsksAgain()
    {
        int cleanups = 0;
        int confirmations = 0;
        var coordinator = new ShutdownCoordinator(
            () =>
            {
                cleanups++;
                return ValueTask.CompletedTask;
            },
            () => { },
            new CollectingLogger<ShutdownCoordinator>(),
            () =>
            {
                confirmations++;
                return Task.FromResult(false);
            });

        var first = new ShutdownRequestedEventArgs();
        var second = new ShutdownRequestedEventArgs();
        coordinator.OnShutdownRequested(first);
        await Task.Yield();
        coordinator.OnShutdownRequested(second);

        Assert.True(first.Cancel);
        Assert.True(second.Cancel);
        Assert.Equal(2, confirmations);
        Assert.Equal(0, cleanups);
    }

    [Fact]
    public async Task AnAcceptedConfirmationRunsCleanupThenShutdown()
    {
        var shutdownCalled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int cleanups = 0;
        var coordinator = new ShutdownCoordinator(
            () =>
            {
                cleanups++;
                return ValueTask.CompletedTask;
            },
            () => shutdownCalled.TrySetResult(),
            new CollectingLogger<ShutdownCoordinator>(),
            () => Task.FromResult(true));

        var request = new ShutdownRequestedEventArgs();
        coordinator.OnShutdownRequested(request);
        await shutdownCalled.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.True(request.Cancel);
        Assert.Equal(1, cleanups);
    }

    [Fact]
    public async Task AThrowingConfirmationIsLoggedAndTheApplicationKeepsRunning()
    {
        var failure = new InvalidOperationException("confirm boom");
        int shutdownCalls = 0;
        int cleanups = 0;
        var logger = new CollectingLogger<ShutdownCoordinator>();
        var coordinator = new ShutdownCoordinator(
            () =>
            {
                cleanups++;
                return ValueTask.CompletedTask;
            },
            () => shutdownCalls++,
            logger,
            () => throw failure);

        var request = new ShutdownRequestedEventArgs();
        coordinator.OnShutdownRequested(request);
        await Task.Yield();
        var next = new ShutdownRequestedEventArgs();
        coordinator.OnShutdownRequested(next);

        Assert.True(request.Cancel);
        Assert.True(next.Cancel);
        Assert.Equal(0, shutdownCalls);
        Assert.Equal(0, cleanups);
        Assert.All(logger.Entries, entry => Assert.Equal(1026, entry.EventId.Id));
        Assert.Same(failure, logger.Entries[0].Exception);
    }
}
