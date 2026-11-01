using System.Diagnostics;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Hosting.Avalonia;
using NetPrints.Projects;
using NetPrints.Testing;

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

    [Fact]
    public async Task TheArgumentListReachesTheProgramExactly()
    {
        Assert.True(File.Exists(RunFixtures.EchoToolDll()), RunFixtures.EchoToolDll());
        string[] arguments = ["", "a b", "say \"hi\"", "back\\slash\\", "\\\"", "'single'", "$HOME;&|", "ñandú 日本語 😀"];

        await using var run = EchoRun.Start(new ProcessLauncher(), arguments);
        await run.Exited;

        Assert.Equal($"argc={arguments.Length}", run.Lines.Single(l => l.StartsWith("argc=", StringComparison.Ordinal)));
        for (int i = 0; i < arguments.Length; i++)
        {
            Assert.Contains($"argv[{i}]={arguments[i]}", run.Lines);
        }
    }

    [Fact]
    public async Task AWorkingDirectoryWithSpacesIsHonoured()
    {
        string folder = Directory.CreateTempSubdirectory("np echo dir ").FullName;
        try
        {
            await using var run = EchoRun.Start(new ProcessLauncher(), [], workingDirectory: folder);
            await run.Exited;

            Assert.Contains(run.Lines, l => l.StartsWith("cwd=", StringComparison.Ordinal) && l.EndsWith(Path.GetFileName(folder), StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task RemovedVariablesDoNotReachTheProgram()
    {
        const string Name = "NETPRINTS_REMOVE_ME";
        Environment.SetEnvironmentVariable(Name, "inherited");
        try
        {
            await using var run = EchoRun.Start(
                new ProcessLauncher(),
                [],
                configure: r => r with { EnvironmentRemovals = [Name], EnvironmentVariables = new Dictionary<string, string> { ["ECHO_TOOL_ENV"] = Name } });
            await run.Exited;

            Assert.Contains($"env:{Name} unset", run.Lines);
        }
        finally
        {
            Environment.SetEnvironmentVariable(Name, null);
        }
    }

    [Fact]
    public async Task AVariableIsAppliedAfterItsRemoval()
    {
        const string Name = "NETPRINTS_REMOVE_THEN_SET";
        Environment.SetEnvironmentVariable(Name, "inherited");
        try
        {
            await using var run = EchoRun.Start(
                new ProcessLauncher(),
                [],
                configure: r => r with
                {
                    EnvironmentRemovals = [Name],
                    EnvironmentVariables = new Dictionary<string, string> { ["ECHO_TOOL_ENV"] = Name, [Name] = "applied" },
                });
            await run.Exited;

            Assert.Contains($"env:{Name}=applied", run.Lines);
        }
        finally
        {
            Environment.SetEnvironmentVariable(Name, null);
        }
    }

    [Fact]
    public async Task InputLinesAreEchoedAndClosingTheInputEndsTheReadLoop()
    {
        await using var run = EchoRun.Start(
            new ProcessLauncher(),
            [],
            configure: r => r with { RedirectInput = true, EnvironmentVariables = new Dictionary<string, string> { ["ECHO_TOOL_MODE"] = "read-line" } });
        await run.WaitForLineAsync("ready");
        Assert.True(run.Handle.CanWriteInput);

        await run.Handle.WriteLineAsync("first line", TestContext.Current.CancellationToken);
        await run.Handle.WriteLineAsync("ñandú \"2\"", TestContext.Current.CancellationToken);
        await run.WaitForLineAsync("stdin=ñandú \"2\"");
        Assert.Contains("stdin=first line", run.Lines);

        run.Handle.CloseInput();
        run.Handle.CloseInput();
        Assert.False(run.Handle.CanWriteInput);
        await run.WaitForLineAsync("stdin-eof");
        await run.Exited;
        await Assert.ThrowsAsync<InvalidOperationException>(() => run.Handle.WriteLineAsync("late", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WritingWithoutRedirectedInputIsRejected()
    {
        await using var run = EchoRun.Start(new ProcessLauncher(), []);

        Assert.False(run.Handle.CanWriteInput);
        await Assert.ThrowsAsync<InvalidOperationException>(() => run.Handle.WriteLineAsync("x", TestContext.Current.CancellationToken));
        await run.Exited;
    }

    [Fact]
    public async Task CanWriteInputTurnsFalseOnExitAndRaisesTheChange()
    {
        await using var run = EchoRun.Start(
            new ProcessLauncher(),
            [],
            configure: r => r with { RedirectInput = true, EnvironmentVariables = new Dictionary<string, string> { ["ECHO_TOOL_MODE"] = "sleep" } });
        await run.WaitForLineAsync("ready");
        Assert.True(run.Handle.CanWriteInput);
        int changes = 0;
        run.Handle.InputStateChanged += (_, _) => Interlocked.Increment(ref changes);

        run.Handle.Kill();
        await run.Exited;

        Assert.False(run.Handle.CanWriteInput);
        Assert.True(run.Handle.HasExited);
        Assert.True(Volatile.Read(ref changes) >= 1);
    }

    [Fact]
    public async Task WithoutCaptureThereAreNoLinesButTheStartAndTheExitAreReported()
    {
        string outFolder = Directory.CreateTempSubdirectory("np echo out ").FullName;
        string outFile = Path.Combine(outFolder, "out.txt");
        try
        {
            await using var run = EchoRun.Start(
                new ProcessLauncher(),
                ["x"],
                configure: r => r with
                {
                    CaptureOutput = false,
                    RedirectInput = true,
                    EnvironmentVariables = new Dictionary<string, string> { ["ECHO_TOOL_OUT"] = outFile },
                });
            await run.Exited;

            Assert.True(run.Started);
            Assert.Empty(run.Lines);
            Assert.False(run.Handle.CanWriteInput);
            Assert.Contains("argv[0]=x", await File.ReadAllLinesAsync(outFile, TestContext.Current.CancellationToken));
        }
        finally
        {
            Directory.Delete(outFolder, recursive: true);
        }
    }

    [Fact]
    public async Task KillIsIdempotent()
    {
        await using var run = EchoRun.Start(
            new ProcessLauncher(),
            [],
            configure: r => r with { EnvironmentVariables = new Dictionary<string, string> { ["ECHO_TOOL_MODE"] = "sleep" } });
        await run.WaitForLineAsync("ready");

        run.Handle.Kill();
        run.Handle.Kill();
        await run.Exited;
        run.Handle.Kill();

        Assert.True(run.Handle.HasExited);
    }

    [Fact]
    public async Task AProgramThatCannotBeStartedThrowsFromStartWithoutReportingAStart()
    {
        var launcher = new ProcessLauncher();
        bool started = false;
        launcher.ProcessStarted += (_, _) => started = true;

        Assert.ThrowsAny<Exception>(() => launcher.Start(new("np-no-such-program-1f3a", [], Environment.CurrentDirectory), TestContext.Current.CancellationToken));
        Assert.ThrowsAny<Exception>(() => launcher.Start(new(RunFixtures.DotnetHost(), [], Path.Combine(Path.GetTempPath(), "np-no-such-dir-1f3a")), TestContext.Current.CancellationToken));
        Assert.False(started);
        await Task.CompletedTask;
    }

    private sealed class EchoRun : IAsyncDisposable
    {
        private readonly TaskCompletionSource exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly List<string> lines = [];
        private readonly CancellationTokenSource cancellation = new();

        private EchoRun(ProcessLauncher launcher, ProcessStartRequest request)
        {
            launcher.ProcessStarted += (_, _) => Started = true;
            launcher.LineReceived += (_, _, line) =>
            {
                lock (lines)
                {
                    lines.Add(line);
                }
            };
            launcher.ProcessExited += (_, _) => exited.TrySetResult();
            Handle = launcher.Start(request, cancellation.Token);
        }

        public IRunningProcess Handle { get; }

        public bool Started { get; private set; }

        public Task Exited => exited.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        public IReadOnlyList<string> Lines
        {
            get
            {
                lock (lines)
                {
                    return [.. lines];
                }
            }
        }

        public static EchoRun Start(
            ProcessLauncher launcher,
            string[] arguments,
            string? workingDirectory = null,
            Func<ProcessStartRequest, ProcessStartRequest>? configure = null)
        {
            var request = new ProcessStartRequest(RunFixtures.DotnetHost(), [RunFixtures.EchoToolDll(), .. arguments], workingDirectory ?? Environment.CurrentDirectory);
            return new EchoRun(launcher, configure?.Invoke(request) ?? request);
        }

        public async Task WaitForLineAsync(string expected)
        {
            var deadline = Stopwatch.StartNew();
            while (!Lines.Contains(expected))
            {
                Assert.True(deadline.Elapsed < TimeSpan.FromSeconds(30), $"never saw '{expected}'; got: {string.Join(" | ", Lines)}");
                await Task.Delay(10, TestContext.Current.CancellationToken);
            }
        }

        public async ValueTask DisposeAsync()
        {
            await cancellation.CancelAsync();
            cancellation.Dispose();
        }
    }
}
