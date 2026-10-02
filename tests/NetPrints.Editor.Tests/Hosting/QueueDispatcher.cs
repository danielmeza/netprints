using NetPrints.Editor.Hosting;

namespace NetPrints.Editor.Tests.Hosting;

/// <summary>A dispatcher that keeps what is posted until <see cref="Flush"/> runs it, like a UI thread that is busy.</summary>
public sealed class QueueDispatcher : IUiDispatcher
{
    private readonly Queue<Action> queue = new();

    public int Pending => queue.Count;

    public void Post(Action action) => queue.Enqueue(action);

    public Task InvokeAsync(Action action)
    {
        queue.Enqueue(action);
        return Task.CompletedTask;
    }

    public bool CheckAccess() => false;

    public void Flush()
    {
        while (queue.TryDequeue(out Action? action))
        {
            action();
        }
    }
}
