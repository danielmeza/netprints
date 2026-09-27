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
    /// Runs <paramref name="task"/> to completion without awaiting it. If it faults, logs the
    /// exception (1030) and shows it in the error dialog on the UI thread through
    /// <paramref name="context"/> — the same report path a command reaches when invoked through
    /// <c>Execute</c> (CommunityToolkit.Mvvm's default await-and-rethrow-on-the-calling-context
    /// behavior). For a fire-and-forget call that is not a command, so nothing else would observe
    /// its fault otherwise.
    /// </summary>
    /// <param name="task">Task to run to completion without awaiting.</param>
    /// <param name="context">Used to log the fault and show the error dialog.</param>
    /// <param name="failureTitle">Error dialog title.</param>
    public static void Forget(this Task task, EditorContext context, string failureTitle)
    {
        ArgumentNullException.ThrowIfNull(context);
        var logger = context.LoggerFactory.CreateLogger(typeof(TaskExtensions).FullName ?? nameof(TaskExtensions));
        task.Forget(exception =>
        {
            Log.TaskFaulted(logger, exception);
            context.Dispatcher.Post(() => context.Dialogs.ShowErrorAsync(failureTitle, exception.ToString()).Forget(logger));
        });
    }

    /// <summary>
    /// Runs <paramref name="task"/> to completion without awaiting it, invoking
    /// <paramref name="onFaulted"/> once per inner exception if it faults. For a caller that reports
    /// faults its own way instead of through an <see cref="ILogger"/>.
    /// </summary>
    /// <param name="task">Task to run to completion without awaiting.</param>
    /// <param name="onFaulted">Called with each of the task's (unwrapped) inner exceptions if it faults.</param>
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
        if (!task.IsFaulted || task.Exception is not { } exception)
        {
            return;
        }

        var innerExceptions = exception.Flatten().InnerExceptions;
        if (innerExceptions.Count == 0)
        {
            onFaulted(exception);
            return;
        }

        foreach (var innerException in innerExceptions)
        {
            onFaulted(innerException);
        }
    }
}
