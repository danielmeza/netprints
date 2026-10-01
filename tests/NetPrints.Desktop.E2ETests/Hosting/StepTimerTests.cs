namespace NetPrints.Desktop.E2ETests.Hosting;

/// <summary><see cref="StepTimer"/>'s forced timeout: holding a step, and failing loudly when none was held.</summary>
public sealed class StepTimerTests
{
    [Fact]
    public void AForcedStepThatNeverReachedACheckpointFailsLoudly()
    {
        var steps = new StepTimer("test", "compile");
        using (steps.Step("compile"))
        {
        }

        var failure = Assert.Throws<InvalidOperationException>(steps.EnsureForcedStepHeld);

        Assert.Contains("step 'compile' was never held", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AForcedStepThatWasHeldPasses()
    {
        var steps = new StepTimer("test", "start");
        using var cts = new CancellationTokenSource();
        Task hold;
        using (steps.Step("start"))
        {
            hold = steps.HoldIfForcedAsync(cts.Token);
        }

        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => hold);

        steps.EnsureForcedStepHeld();
    }

    [Fact]
    public void NoForcedStepIsNeverAFailure()
    {
        var steps = new StepTimer("test");

        steps.EnsureForcedStepHeld();
    }
}
