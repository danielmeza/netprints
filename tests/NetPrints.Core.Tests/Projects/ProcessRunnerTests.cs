using System;
using System.Threading;
using System.Threading.Tasks;
using NetPrints.Projects;
using Xunit;

namespace NetPrints.Tests.Projects
{
    /// <summary>
    /// <see cref="ProcessRunner"/> (project-system.md §4): exit code and both output streams are
    /// captured, including the "read both streams concurrently, don't drain one at a time" pattern
    /// sub-phase E's notes call out (a naive implementation deadlocks once one stream's OS pipe buffer
    /// fills while the other is still being read), and cancellation kills the process.
    /// </summary>
    public class ProcessRunnerTests
    {
        [Fact]
        public async Task RunAsyncCapturesExitCodeAndBothStreams()
        {
            var runner = new ProcessRunner();
            var request = new ProcessStartRequest("/bin/sh", ["-c", "echo out-line; echo err-line 1>&2; exit 3"], "/tmp");

            ProcessResult result = await runner.RunAsync(request, TestContext.Current.CancellationToken);

            Assert.Equal(3, result.ExitCode);
            Assert.Contains("out-line", result.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("err-line", result.StandardError, StringComparison.Ordinal);
        }

        [Fact]
        public async Task RunAsyncDoesNotDeadlockOnLargeConcurrentOutput()
        {
            var runner = new ProcessRunner();
            // Enough on each stream to fill an OS pipe buffer (typically 64KiB) many times over: a
            // runner that reads stdout and stderr one after another (instead of concurrently) hangs here.
            var request = new ProcessStartRequest("/bin/sh",
                ["-c", "head -c 1000000 /dev/zero; head -c 1000000 /dev/zero 1>&2"], "/tmp");

            ProcessResult result = await runner.RunAsync(request, TestContext.Current.CancellationToken);

            Assert.Equal(0, result.ExitCode);
            Assert.Equal(1_000_000, result.StandardOutput.Length);
            Assert.Equal(1_000_000, result.StandardError.Length);
        }

        [Fact]
        public async Task RunAsyncKillsTheProcessWhenCancelled()
        {
            var runner = new ProcessRunner();
            var request = new ProcessStartRequest("/bin/sh", ["-c", "sleep 30"], "/tmp");
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => runner.RunAsync(request, cts.Token));
        }
    }
}
