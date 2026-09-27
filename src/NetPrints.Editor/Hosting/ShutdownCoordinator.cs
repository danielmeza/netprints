using Avalonia.Controls.ApplicationLifetimes;
using Microsoft.Extensions.Logging;

namespace NetPrints.Editor.Hosting;

/// <summary>
/// Coalesces repeated <c>ShutdownRequested</c> events into a single cleanup pass: the first
/// request cancels and starts cleanup, later requests made while cleanup is running cancel and
/// reuse that same task, and once cleanup has finished a request passes through so the process
/// can exit.
/// </summary>
public sealed class ShutdownCoordinator
{
    private readonly Func<ValueTask> cleanUpAsync;
    private readonly Action shutdown;
    private readonly ILogger logger;
    private bool cleanedUp;
    private Task? cleanup;

    /// <summary>
    /// Creates a coordinator that runs <paramref name="cleanUpAsync"/> at most once, then calls
    /// <paramref name="shutdown"/> whether or not it threw.
    /// </summary>
    /// <param name="cleanUpAsync">Disposes host services and any other per-run cleanup.</param>
    /// <param name="shutdown">Lets shutdown proceed once cleanup has finished.</param>
    /// <param name="logger">Logger for a cleanup failure (<see cref="Log.ShutdownCleanupFailed"/>).</param>
    public ShutdownCoordinator(Func<ValueTask> cleanUpAsync, Action shutdown, ILogger logger)
    {
        this.cleanUpAsync = cleanUpAsync;
        this.shutdown = shutdown;
        this.logger = logger;
    }

    /// <summary>
    /// Handles one shutdown request: cancels it and starts (or reuses) the cleanup task, unless
    /// cleanup has already finished, in which case the request is left to proceed.
    /// </summary>
    /// <param name="e">The event args from <c>IClassicDesktopStyleApplicationLifetime.ShutdownRequested</c>.</param>
    public void OnShutdownRequested(ShutdownRequestedEventArgs e)
    {
        if (cleanedUp)
        {
            return;
        }

        e.Cancel = true;
        cleanup ??= CleanUpAndShutdownAsync();
    }

    private async Task CleanUpAndShutdownAsync()
    {
        try
        {
            await cleanUpAsync();
        }
        catch (Exception ex)
        {
            Log.ShutdownCleanupFailed(logger, ex);
        }
        finally
        {
            cleanedUp = true;
            shutdown();
        }
    }
}
