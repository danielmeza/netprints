using System.Reactive.Concurrency;
using System.Reactive.Disposables;

namespace NetPrints.Editor.Hosting;

/// <summary>
/// Rx scheduler that runs work through an <see cref="IUiDispatcher"/> (inline in tests).
/// </summary>
public sealed class UiDispatcherScheduler(IUiDispatcher dispatcher) : LocalScheduler
{
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
