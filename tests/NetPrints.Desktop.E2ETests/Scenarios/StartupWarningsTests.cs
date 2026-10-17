using NetPrints.Desktop.E2ETests.Hosting;
using NetPrints.Editor.Contributions.BuiltIn;

namespace NetPrints.Desktop.E2ETests.Scenarios;

/// <summary>The editor starts and shows its panels without Dock binding warnings (FR-088): no <c>IsPinned</c> warning and no capability "Value is null".</summary>
public sealed class StartupWarningsTests(DesktopWorkerPool pool) : ProjectEditorTestBase(pool)
{
    private const string BindingWarning = "An error occurred binding";

    [Fact]
    public Task StartingAndShowingThePanelsLogsNoDockBindingWarnings() => RunScenarioAsync(async token =>
    {
        await StartAsync(token);

        using (Step("the project opens and the bottom panel shows"))
        {
            await WaitForProjectAsync(token);
            await Shell.Bottom.ShowAsync(PanelContributions.ErrorsId, token);
        }

        using (Step("the log has no binding warning"))
        {
            string output = LeasedEditor.Output;
            Assert.DoesNotContain("IsPinned", output, StringComparison.Ordinal);
            Assert.DoesNotContain("Value is null", output, StringComparison.Ordinal);
            Assert.DoesNotContain(BindingWarning, output, StringComparison.Ordinal);
        }
    });
}
