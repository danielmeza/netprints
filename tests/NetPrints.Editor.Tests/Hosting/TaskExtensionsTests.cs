using Microsoft.Extensions.Logging;
using NetPrints.Editor.Hosting;

namespace NetPrints.Editor.Tests.Hosting;

/// <summary>
/// <see cref="TaskExtensions.Forget(Task, ILogger)"/>: a discarded task's fault is observed and
/// logged (1030) instead of becoming an unobserved task exception (AGENTS.md "C# rules": "Task
/// discards only through Forget").
/// </summary>
public class TaskExtensionsTests
{
    [Fact]
    public void ForgetLogsAnAlreadyFaultedTasksException()
    {
        var logger = new CollectingLogger<TaskExtensionsTests>();
        var exception = new InvalidOperationException("boom");

        Task.FromException(exception).Forget(logger);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(1030, entry.EventId.Id);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Same(exception, entry.Exception);
    }

    [Fact]
    public void ForgetDoesNotLogASuccessfullyCompletedTask()
    {
        var logger = new CollectingLogger<TaskExtensionsTests>();

        Task.CompletedTask.Forget(logger);

        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task ForgetReportsATaskThatFaultsAfterBeingForgotten()
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reported = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var exception = new InvalidOperationException("boom");
        Exception? observed = null;

        tcs.Task.Forget(ex =>
        {
            observed = ex;
            reported.SetResult();
        });
        tcs.SetException(exception);

        await reported.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Same(exception, observed);
    }
}
