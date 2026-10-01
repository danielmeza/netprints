using System.Text.Json;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Projects;

namespace NetPrints.Editor.Tests.Hosting;

/// <summary>
/// <see cref="RunStateTracker"/>: the state of the last launched program, kept so a failing E2E
/// test can tell a slow run from lost output (issue #11).
/// </summary>
public class RunStateTrackerTests
{
    private static readonly ProcessStartRequest Request = new("dotnet", ["run"], "/work");

    private readonly FakeProcessLauncher launcher = new();

    [Fact]
    public void StartsNotStarted()
    {
        using var tracker = new RunStateTracker(launcher);

        var state = tracker.Snapshot();

        Assert.Equal(RunPhase.NotStarted, state.Phase);
        Assert.Null(state.ExitCode);
        Assert.Empty(state.Stdout);
        Assert.Empty(state.Stderr);
    }

    [Fact]
    public void GoesFromBuildingToRunningToExitedWithTheExitCode()
    {
        using var tracker = new RunStateTracker(launcher);

        tracker.BuildStarted();
        Assert.Equal(RunPhase.Building, tracker.Snapshot().Phase);

        tracker.BuildFinished();
        launcher.Start(Request, TestContext.Current.CancellationToken);
        launcher.RaiseLine(ProcessStream.Output, "Hello");
        launcher.RaiseLine(ProcessStream.Error, "warn");
        var running = tracker.Snapshot();
        Assert.Equal(RunPhase.Running, running.Phase);
        Assert.Equal(["Hello"], running.Stdout);
        Assert.Equal(["warn"], running.Stderr);

        launcher.RaiseExited(3);
        var exited = tracker.Snapshot();
        Assert.Equal(RunPhase.Exited, exited.Phase);
        Assert.Equal(3, exited.ExitCode);
        Assert.Equal(["Hello"], exited.Stdout);
    }

    [Fact]
    public void KeepsOnlyTheLast200LinesOfEachStream()
    {
        using var tracker = new RunStateTracker(launcher);
        launcher.Start(Request, TestContext.Current.CancellationToken);

        for (int i = 0; i < 250; i++)
        {
            launcher.RaiseLine(ProcessStream.Output, $"out {i}");
            launcher.RaiseLine(ProcessStream.Error, $"err {i}");
        }

        var state = tracker.Snapshot();
        Assert.Equal(200, state.Stdout.Count);
        Assert.Equal("out 50", state.Stdout[0]);
        Assert.Equal("out 249", state.Stdout[^1]);
        Assert.Equal(200, state.Stderr.Count);
        Assert.Equal("err 249", state.Stderr[^1]);
    }

    [Fact]
    public void ANewCompileResetsTheState()
    {
        using var tracker = new RunStateTracker(launcher);
        launcher.Start(Request, TestContext.Current.CancellationToken);
        launcher.RaiseLine(ProcessStream.Output, "old");
        launcher.RaiseExited(0);

        tracker.BuildStarted();

        var state = tracker.Snapshot();
        Assert.Equal(RunPhase.Building, state.Phase);
        Assert.Null(state.ExitCode);
        Assert.Empty(state.Stdout);
    }

    [Fact]
    public void ANewRunResetsTheTailsAndTheExitCode()
    {
        using var tracker = new RunStateTracker(launcher);
        launcher.Start(Request, TestContext.Current.CancellationToken);
        launcher.RaiseLine(ProcessStream.Error, "old");
        launcher.RaiseExited(1);

        launcher.Start(Request, TestContext.Current.CancellationToken);

        var state = tracker.Snapshot();
        Assert.Equal(RunPhase.Running, state.Phase);
        Assert.Null(state.ExitCode);
        Assert.Empty(state.Stderr);
    }

    [Fact]
    public void AnExitOfAnEarlierProgramDoesNotEndTheCurrentOne()
    {
        using var tracker = new RunStateTracker(launcher);
        launcher.Start(Request, TestContext.Current.CancellationToken);
        int first = launcher.LastId;
        launcher.Start(Request, TestContext.Current.CancellationToken);

        launcher.RaiseLine(ProcessStream.Output, "stale", first);
        launcher.RaiseExited(7, first);
        launcher.RaiseLine(ProcessStream.Output, "current");

        var running = tracker.Snapshot();
        Assert.Equal(RunPhase.Running, running.Phase);
        Assert.Null(running.ExitCode);
        Assert.Equal(["current"], running.Stdout);

        launcher.RaiseExited(0);
        Assert.Equal(RunPhase.Exited, tracker.Snapshot().Phase);
        Assert.Equal(0, tracker.Snapshot().ExitCode);
    }

    [Fact]
    public void AFinishedBuildLeavesARunningProgramAlone()
    {
        using var tracker = new RunStateTracker(launcher);
        launcher.Start(Request, TestContext.Current.CancellationToken);

        tracker.BuildFinished();

        Assert.Equal(RunPhase.Running, tracker.Snapshot().Phase);
    }

    [Fact]
    public void AFailedBuildReturnsToNotStarted()
    {
        using var tracker = new RunStateTracker(launcher);
        tracker.BuildStarted();

        tracker.BuildFinished();

        Assert.Equal(RunPhase.NotStarted, tracker.Snapshot().Phase);
    }

    [Fact]
    public void StopsListeningOnceDisposed()
    {
        var tracker = new RunStateTracker(launcher);
        tracker.Dispose();

        launcher.Start(Request, TestContext.Current.CancellationToken);

        Assert.Equal(RunPhase.NotStarted, tracker.Snapshot().Phase);
    }

    [Fact]
    public void TheRunStateReplySerializesThroughTheAutomationContext()
    {
        var reply = new AutomationResponse(true) { RunState = new RunStateSnapshot(RunPhase.Exited, 2, ["out"], ["err"]) };

        string json = JsonSerializer.Serialize(reply, AutomationJsonContext.Default.AutomationResponse);
        var back = JsonSerializer.Deserialize(json, AutomationJsonContext.Default.AutomationResponse);

        Assert.Contains("\"phase\":\"exited\"", json, StringComparison.Ordinal);
        Assert.Equal(RunPhase.Exited, back?.RunState?.Phase);
        Assert.Equal(2, back?.RunState?.ExitCode);
        Assert.Equal(["out"], back?.RunState?.Stdout);
        Assert.Equal(["err"], back?.RunState?.Stderr);
        Assert.Equal("{\"ok\":true,\"runState\":{\"phase\":\"notStarted\",\"stdout\":[],\"stderr\":[]}}",
            JsonSerializer.Serialize(new AutomationResponse(true) { RunState = new RunStateSnapshot(RunPhase.NotStarted, null, [], []) }, AutomationJsonContext.Default.AutomationResponse));
    }

    [Fact]
    public void TheRunStateRequestIsKnownToTheProtocol()
    {
        var request = JsonSerializer.Deserialize("{\"op\":\"runState\"}", AutomationJsonContext.Default.AutomationRequest);

        Assert.Equal("runState", request?.Op);
    }
}
