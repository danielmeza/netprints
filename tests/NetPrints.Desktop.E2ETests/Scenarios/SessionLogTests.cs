using NetPrints.Desktop.E2ETests.Hosting;
using NetPrints.Editor.State;

namespace NetPrints.Desktop.E2ETests.Scenarios;

/// <summary>The editor writes one session log file in the log folder under the harness's state folder, with the scenario's expected line (FR-106, ADR-0033).</summary>
public sealed class SessionLogTests(DesktopWorkerPool pool) : ProjectEditorTestBase(pool)
{
    private const string LayoutFile = "layout.json";
    private const string SessionFilePattern = "netprints-*.log";
    private const string WarningLevel = "WARN";
    private const string StateStoreCategory = "NetPrints.Editor.State.JsonEditorStateStore";

    protected override EditorStart EditorStartFor(string sampleProject)
    {
        string state = new EditorDataPaths(StateDirectory).StateDirectory;
        Directory.CreateDirectory(state);
        File.WriteAllText(Path.Combine(state, LayoutFile), "{");
        return base.EditorStartFor(sampleProject);
    }

    [Fact]
    public Task TheSessionLogHoldsTheScenariosWarningInTheHarnessLogFolder() => RunScenarioAsync(async token =>
    {
        await StartAsync(token);
        await WaitForProjectAsync(token);

        using (Step("one session file is in the log folder of the harness state folder"))
        {
            string logs = new EditorDataPaths(StateDirectory).LogDirectory;
            string file = Assert.Single(Directory.GetFiles(logs, SessionFilePattern));
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            string text = await reader.ReadToEndAsync(token);

            string line = Assert.Single(text.Split('\n'), candidate =>
                candidate.Contains(WarningLevel, StringComparison.Ordinal)
                && candidate.Contains(StateStoreCategory, StringComparison.Ordinal)
                && candidate.Contains(LayoutFile, StringComparison.Ordinal));
            Assert.NotEmpty(line);
        }
    });
}
