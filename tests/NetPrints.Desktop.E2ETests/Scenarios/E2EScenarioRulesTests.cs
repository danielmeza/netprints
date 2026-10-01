using NetPrints.Desktop.E2ETests.Hosting;
using Xunit.Sdk;

namespace NetPrints.Desktop.E2ETests.Scenarios;

/// <summary>How <c>RunScenarioAsync</c> treats a skip and a forced step that never reached a checkpoint, on the real editor.</summary>
public sealed class E2EScenarioRulesTests(DesktopWorkerPool pool) : X11SmokeTestBase(pool)
{
    protected override string? ForcedTimeoutStep => "never held";

    protected override TimeSpan Budget => TimeSpan.FromSeconds(60);

    [Fact]
    public async Task AForcedStepWithoutACheckpointFailsTheScenario()
    {
        var failure = await Assert.ThrowsAsync<E2EStepFailureException>(() => RunScenarioAsync(async token =>
        {
            await StartAsync(token);
            using (Step("never held"))
            {
            }
        }));

        Assert.Contains("step 'never held' was never held", failure.InnerException?.Message, StringComparison.Ordinal);
    }
}

/// <summary>A skip raised after the editor started is a skip, not an <see cref="E2EStepFailureException"/>.</summary>
public sealed class E2ESkipAfterStartTests(DesktopWorkerPool pool) : X11SmokeTestBase(pool)
{
    protected override TimeSpan Budget => TimeSpan.FromSeconds(60);

    [Fact]
    public async Task ASkipAfterStartPassesThroughTheCapture()
    {
        bool skipped = false;
        try
        {
            await RunScenarioAsync(async token =>
            {
                await StartAsync(token);
                Assert.Skip("not supported here");
            });
        }
        catch (SkipException)
        {
            skipped = true;
        }

        Assert.True(skipped);
    }
}
