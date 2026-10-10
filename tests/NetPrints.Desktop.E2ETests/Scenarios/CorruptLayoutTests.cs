using NetPrints.Desktop.E2ETests.Hosting;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.State;

namespace NetPrints.Desktop.E2ETests.Scenarios;

/// <summary>A <c>layout.json</c> that is not valid JSON: the editor starts with the default layout and logs a warning, and the project still opens (FR-051, SC-005).</summary>
public sealed class CorruptLayoutTests(DesktopWorkerPool pool) : ProjectEditorTestBase(pool)
{
    private const string LayoutFile = "layout.json";
    private const string Warning = "warn: NetPrints.Editor.State.JsonEditorStateStore[1201]";

    protected override EditorStart EditorStartFor(string sampleProject)
    {
        string state = new EditorDataPaths(StateDirectory).StateDirectory;
        Directory.CreateDirectory(state);
        File.WriteAllText(Path.Combine(state, LayoutFile), "{");
        return base.EditorStartFor(sampleProject);
    }

    [Fact]
    public Task AnUnreadableLayoutFallsBackToTheDefaultLayoutWithAWarning() => RunScenarioAsync(async token =>
    {
        await StartAsync(token);

        using (Step("the project opens in the default layout"))
        {
            await WaitForProjectAsync(token);
            await Shell.PanelContent(PanelContributions.ProjectTreeId).WaitVisibleAsync(token);
            await Shell.Inspector.WaitVisibleAsync(token);
            await Shell.Bottom.ShowAsync(PanelContributions.ErrorsId, token);
        }

        using (Step("the log has the warning"))
        {
            string output = LeasedEditor.Output;
            Assert.Contains(Warning, output, StringComparison.Ordinal);
            Assert.Contains(LayoutFile, output[output.IndexOf(Warning, StringComparison.Ordinal)..], StringComparison.Ordinal);
        }
    });
}
