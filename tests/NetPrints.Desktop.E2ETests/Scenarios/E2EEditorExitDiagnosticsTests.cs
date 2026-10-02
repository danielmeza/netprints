using System.Diagnostics;
using System.Text.Json;
using NetPrints.Desktop.E2ETests.Hosting;

namespace NetPrints.Desktop.E2ETests.Scenarios;

/// <summary>
/// Proves the "editor exited" trigger of the failure diagnostics (contracts/ci.md §3, US1 scenario 2)
/// on the real editor: kills it while the test holds the lease and checks that the failure arrives
/// at once and that the files record the exit code instead of a UI tree.
/// </summary>
public sealed class E2EEditorExitDiagnosticsTests(DesktopWorkerPool pool) : X11SmokeTestBase(pool)
{
    protected override TimeSpan Budget => TimeSpan.FromSeconds(90);

    [Fact]
    public async Task AnEditorThatExitsWhileLeasedLeavesItsExitCode()
    {
        var clock = new Stopwatch();
        var failure = await Assert.ThrowsAsync<E2EStepFailureException>(() => RunScenarioAsync(async token =>
        {
            await StartAsync(token);
            clock.Start();
            using (Step("kill editor"))
            {
                LeasedEditor.Kill();
                await Task.Delay(System.Threading.Timeout.InfiniteTimeSpan, token);
            }
        }));

        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(30), $"The failure took {clock.Elapsed.TotalSeconds:0} s, after the kill, not well before the budget.");
        Assert.StartsWith("[step 'kill editor' running for ", failure.Message, StringComparison.Ordinal);
        string folder = FailureCapture.FolderFor(nameof(E2EEditorExitDiagnosticsTests));
        Assert.Contains("failure: editor exited", await File.ReadAllTextAsync(Path.Combine(folder, "summary.md"), TestContext.Current.CancellationToken), StringComparison.Ordinal);
        string process = (await File.ReadAllTextAsync(Path.Combine(folder, "process.txt"), TestContext.Current.CancellationToken)).Trim();
        Assert.Matches(@"^exited -?\d+$", process);
        string code = process["exited ".Length..];
        using var tree = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(folder, "ui-tree.json"), TestContext.Current.CancellationToken));
        Assert.Equal(code, tree.RootElement.GetProperty("editorExitCode").GetRawText());
        Assert.Equal(0, tree.RootElement.GetProperty("windows").GetArrayLength());
        using var state = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(folder, "run-state.json"), TestContext.Current.CancellationToken));
        Assert.Equal(code, state.RootElement.GetProperty("editorExitCode").GetRawText());
        Assert.False(File.Exists(Path.Combine(folder, FailureCapture.ErrorsFileName)));
    }
}
