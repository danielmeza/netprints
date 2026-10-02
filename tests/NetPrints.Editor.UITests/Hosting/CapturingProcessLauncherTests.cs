using NetPrints.Projects;

namespace NetPrints.Editor.UITests.Hosting;

/// <summary>
/// The headless launcher reports a program's exit only after its output was delivered (issue #11): a line that
/// arrives after the exit is dropped by the run tracker, and the flow then waits for it until its timeout.
/// </summary>
public class CapturingProcessLauncherTests
{
    [Fact]
    public async Task TheExitIsNotReportedWhileTheFirstLineIsStillBeingDelivered()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "needs a POSIX shell");
        using var launcher = new CapturingProcessLauncher();
        using var exitReported = new ManualResetEvent(false);
        var exited = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        bool deliveryFinished = false;

        // the reader is held in the line handler until the exit is reported; a launcher that drains first never reports it, so the wait is bounded
        launcher.LineReceived += (_, _, _) =>
        {
            exitReported.WaitOne(TimeSpan.FromSeconds(1));
            Volatile.Write(ref deliveryFinished, true);
        };
        launcher.ProcessExited += (_, _) =>
        {
            exited.TrySetResult(Volatile.Read(ref deliveryFinished));
            exitReported.Set();
        };

        launcher.Start(new("echo", ["hello"], Environment.CurrentDirectory), TestContext.Current.CancellationToken);

        Assert.True(await exited.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken), "the exit was reported before the output was delivered");
    }
}
