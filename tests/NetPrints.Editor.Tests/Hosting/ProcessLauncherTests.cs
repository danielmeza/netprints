using System.Diagnostics;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Hosting.Avalonia;
using NetPrints.Projects;

namespace NetPrints.Editor.Tests.Hosting;

/// <summary><see cref="ProcessLauncher"/>: the exit is always reported, after the start and without waiting for stray pipe holders.</summary>
public class ProcessLauncherTests
{
    private static readonly ProcessStartRequest Instant = OperatingSystem.IsWindows()
        ? new("cmd", ["/c", "exit", "0"], Environment.CurrentDirectory)
        : new("true", [], Environment.CurrentDirectory);

    [Fact]
    public async Task AnInstantlyExitingProgramAlwaysEndsExited()
    {
        var launcher = new ProcessLauncher();
        using var tracker = new RunStateTracker(launcher);

        for (int i = 0; i < 100; i++)
        {
            launcher.Start(Instant, TestContext.Current.CancellationToken);
            var deadline = Stopwatch.StartNew();
            while (tracker.Snapshot().Phase != RunPhase.Exited)
            {
                Assert.True(deadline.Elapsed < TimeSpan.FromSeconds(10), $"run {i} never reported its exit (phase {tracker.Snapshot().Phase})");
                await Task.Delay(5, TestContext.Current.CancellationToken);
            }
        }
    }

    [Fact]
    public async Task CancellingTheTokenKillsTheProcessTree()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "needs a POSIX shell");
        // a surviving grandchild keeps the pipes open, so with this drain the exit would arrive after the bound
        var launcher = new ProcessLauncher { DrainTimeout = TimeSpan.FromSeconds(30) };
        var exited = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var grandchildStarted = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        launcher.ProcessExited += (_, code) => exited.TrySetResult(code);
        launcher.LineReceived += (_, _, line) => grandchildStarted.TrySetResult(line);
        using var cts = new CancellationTokenSource();

        launcher.Start(new("sh", ["-c", "sleep 60 & echo $!; wait"], Environment.CurrentDirectory), cts.Token);
        await grandchildStarted.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await cts.CancelAsync();

        var done = await Task.WhenAny(exited.Task, Task.Delay(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        Assert.Same(exited.Task, done);
    }

    [Fact]
    public async Task TheExitIsReportedEvenWhenAGrandchildKeepsThePipesOpen()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "needs a POSIX shell");
        var launcher = new ProcessLauncher { DrainTimeout = TimeSpan.FromMilliseconds(500) };
        var exited = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        launcher.ProcessExited += (_, code) => exited.TrySetResult(code);

        launcher.Start(new("sh", ["-c", "sleep 20 & exit 3"], Environment.CurrentDirectory), TestContext.Current.CancellationToken);

        var done = await Task.WhenAny(exited.Task, Task.Delay(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        Assert.Same(exited.Task, done);
        Assert.Equal(3, await exited.Task);
    }
}
