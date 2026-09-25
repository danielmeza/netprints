using System.Reactive.Concurrency;
using System.Reactive.Disposables;

namespace NetPrints.Editor.Hosting;

/// <summary>
/// Rx scheduler that runs work through an <see cref="IUiDispatcher"/> (inline in tests).
/// </summary>
public sealed class UiDispatcherScheduler(IUiDispatcher dispatcher) : LocalScheduler
{
    /// <summary>
    /// Schedules <paramref name="action"/> to run through the constructor's dispatcher: immediately
    /// (posted) if <paramref name="dueTime"/> is zero or negative, otherwise after a
    /// <see cref="Task.Delay(TimeSpan)"/> of that duration. The returned disposable cancels the
    /// action if it has not run yet.
    /// </summary>
    /// <typeparam name="TState">Type of the state passed to <paramref name="action"/>.</typeparam>
    /// <param name="state">State passed to <paramref name="action"/>.</param>
    /// <param name="dueTime">Delay before running <paramref name="action"/>.</param>
    /// <param name="action">Action to run.</param>
    /// <returns>A disposable that cancels the scheduled action.</returns>
    public override IDisposable Schedule<TState>(TState state, TimeSpan dueTime, Func<IScheduler, TState, IDisposable> action)
    {
        var disposable = new SingleAssignmentDisposable();

        void Run()
        {
            if (!disposable.IsDisposed)
            {
                disposable.Disposable = action(this, state);
            }
        }

        if (dueTime <= TimeSpan.Zero)
        {
            dispatcher.Post(Run);
        }
        else
        {
            _ = Task.Delay(dueTime).ContinueWith(_ => dispatcher.Post(Run), TaskScheduler.Default);
        }

        return disposable;
    }
}
