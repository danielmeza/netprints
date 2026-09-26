using System.Runtime.CompilerServices;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;

namespace NetPrints.Editor.Hosting.Avalonia;

/// <summary>
/// Reports exceptions that escape to the UI thread (async void event handlers, unguarded awaits in
/// async commands, unobserved tasks) in the error dialog, so the editor stays usable instead of
/// crashing, and logs them (1001, 1002).
/// </summary>
public sealed class UnhandledExceptionHandler : IDisposable
{
    // An exception from an async void handler reaches the dispatcher (reported there) and also
    // faults the dispatcher operation's task. When that task is finalized, the same exception
    // arrives again as an unobserved task exception; it must not be reported twice.
    private static readonly ConditionalWeakTable<Exception, object> Reported = [];

    private readonly IEditorDialogs dialogs;
    private readonly IUiDispatcher dispatcher;
    private readonly ILogger<UnhandledExceptionHandler> logger;
    private bool reporting;

    /// <summary>
    /// Subscribes to <see cref="Dispatcher.UIThread"/>'s unhandled-exception event and to
    /// <see cref="TaskScheduler.UnobservedTaskException"/>.
    /// </summary>
    /// <param name="dialogs">Used to show the error dialog.</param>
    /// <param name="dispatcher">Used to marshal an unobserved task exception's report to the UI thread.</param>
    /// <param name="logger">Used to log 1001 (UI thread) and 1002 (unobserved task) exceptions.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dialogs"/>, <paramref name="dispatcher"/>
    /// or <paramref name="logger"/> is <see langword="null"/>.</exception>
    public UnhandledExceptionHandler(IEditorDialogs dialogs, IUiDispatcher dispatcher, ILogger<UnhandledExceptionHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(logger);
        this.dialogs = dialogs;
        this.dispatcher = dispatcher;
        this.logger = logger;

        Dispatcher.UIThread.UnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private void OnDispatcherUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        ReportUnhandledUiException(e.Exception);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
        ReportUnobservedTaskException(e.Exception);
    }

    /// <summary>
    /// Logs 1001 and shows the error dialog for <paramref name="exception"/>. Exposed as
    /// <see langword="internal"/> (rather than only reachable through the real
    /// <see cref="Dispatcher.UIThread"/> event, which needs a running dispatcher loop) so
    /// <c>tests/NetPrints.Editor.Tests</c> can exercise it directly (ED-T12); production code only
    /// reaches it through <see cref="OnDispatcherUnhandledException"/>.
    /// </summary>
    /// <param name="exception">The exception that escaped to the UI thread.</param>
    internal void ReportUnhandledUiException(Exception exception)
    {
        Log.UnhandledUiException(logger, exception);
        Reported.AddOrUpdate(exception, Reported);
        _ = ReportAsync(exception);
    }

    /// <summary>
    /// Logs 1002 and, unless every inner exception was already reported as a UI-thread exception,
    /// shows the error dialog for <paramref name="exception"/>. Exposed as <see langword="internal"/>
    /// for the same reason as <see cref="ReportUnhandledUiException"/>: forcing a real, GC-driven
    /// <see cref="TaskScheduler.UnobservedTaskException"/> deterministically in a unit test is
    /// impractical.
    /// </summary>
    /// <param name="exception">The unobserved exception.</param>
    internal void ReportUnobservedTaskException(AggregateException exception)
    {
        Log.UnobservedTaskException(logger, exception);
        if (exception.Flatten().InnerExceptions.All(inner => Reported.TryGetValue(inner, out _)))
        {
            return;
        }

        dispatcher.Post(() => _ = ReportAsync(exception));
    }

    private async Task ReportAsync(Exception exception)
    {
        // An error while showing the error must not loop back into this handler.
        if (reporting)
        {
            return;
        }

        reporting = true;
        try
        {
            await dialogs.ShowErrorAsync("Unexpected error", exception.ToString());
        }
        finally
        {
            reporting = false;
        }
    }

    /// <summary>Unsubscribes from both exception events.</summary>
    public void Dispose()
    {
        Dispatcher.UIThread.UnhandledException -= OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
    }
}
