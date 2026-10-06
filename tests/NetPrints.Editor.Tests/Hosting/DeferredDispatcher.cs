using NetPrints.Editor.Hosting;

namespace NetPrints.Editor.Tests.Hosting;

/// <summary>
/// A dispatcher that runs what is invoked at once until <see cref="Defer"/> is set, then keeps it until <see cref="RunPending"/>:
/// the task of <see cref="InvokeAsync"/> completes only when the action ran, like a busy UI thread.
/// </summary>
public sealed class DeferredDispatcher : IUiDispatcher
{
    private readonly Queue<(Action Action, TaskCompletionSource Done)> queue = new();

    public bool Defer { get; set; }

    public int Pending => queue.Count;

    public void Post(Action action)
    {
        if (Defer)
        {
            queue.Enqueue((action, new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)));
        }
        else
        {
            action();
        }
    }

    public Task InvokeAsync(Action action)
    {
        if (!Defer)
        {
            action();
            return Task.CompletedTask;
        }

        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        queue.Enqueue((action, done));
        return done.Task;
    }

    public bool CheckAccess() => !Defer;

    public void RunPending()
    {
        while (queue.TryDequeue(out (Action Action, TaskCompletionSource Done) item))
        {
            item.Action();
            item.Done.TrySetResult();
        }
    }
}
