using Microsoft.Extensions.Logging;

namespace NetPrints.Editor.Hosting;

/// <summary>
/// Fire-and-forget for a discarded <see cref="Task"/> (AGENTS.md "C# rules": task discards only
/// through <see cref="Forget(Task, ILogger)"/>): observes the task's exception through a
/// continuation, instead of leaving it to become an unobserved task exception, and reports it.
/// </summary>
public static class TaskExtensions
{
    /// <summary>
    /// Runs <paramref name="task"/> to completion without awaiting it, logging its exception through
    /// <paramref name="logger"/> if it faults.
    /// </summary>
    /// <param name="task">Task to run to completion without awaiting.</param>
    /// <param name="logger">Logger a fault is reported to (1030).</param>
    public static void Forget(this Task task, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        task.Forget(exception => Log.TaskFaulted(logger, exception));
    }

    /// <summary>
    /// Runs <paramref name="task"/> to completion without awaiting it, invoking
    /// <paramref name="onFaulted"/> if it faults. For a caller that reports faults its own way
    /// instead of through an <see cref="ILogger"/>.
    /// </summary>
    /// <param name="task">Task to run to completion without awaiting.</param>
    /// <param name="onFaulted">Called with the task's (unwrapped) exception if it faults.</param>
    public static void Forget(this Task task, Action<Exception> onFaulted)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(onFaulted);

        if (task.IsCompleted)
        {
            ReportIfFaulted(task, onFaulted);
            return;
        }

        // The one deliberate exception to "discards only through Forget": this is Forget's own
        // implementation, and the continuation task itself never faults (ReportIfFaulted doesn't throw).
        _ = task.ContinueWith(
            static (completed, state) => ReportIfFaulted(completed, state as Action<Exception>
                ?? throw new InvalidOperationException("Forget's continuation state was not its callback.")),
            onFaulted,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private static void ReportIfFaulted(Task task, Action<Exception> onFaulted)
    {
        if (task.IsFaulted && task.Exception is { } exception)
        {
            onFaulted(exception.Flatten().InnerException ?? exception);
        }
    }
}
