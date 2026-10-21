using NetPrints.Core;
using NetPrints.Desktop.E2ETests.Hosting;
using NetPrints.Testing.Ui.Dialogs;
using NetPrints.Testing.Ui.Shell;

namespace NetPrints.Desktop.E2ETests.Scenarios;

/// <summary>The editor starts with <c>--profile</c>: New project lists the templates of that profile and offers Show all (FR-036, contracts/cli.md section 4).</summary>
public sealed class StartWithProfileTests(DesktopWorkerPool pool) : ProjectEditorTestBase(pool)
{
    protected override EditorStart EditorStartFor(string sampleProject) =>
        new(null, Environment(), WaitForProject: false, Arguments: ["--profile", DefaultProjectProfile.ProfileId]);

    [Fact]
    public Task NewProjectIsFilteredToTheProfileAndShowAllIsOffered() => RunScenarioAsync(async token =>
    {
        await StartAsync(token);
        var start = new StartPagePage(Driver);

        using (Step("the start page shows without an error"))
        {
            await start.WaitVisibleAsync(token);
        }

        using (Step("New project names the profile and offers Show all"))
        {
            await start.NewButton.ClickAsync(token);
            var dialog = new NewProjectDialogPage(Driver);
            await dialog.WaitVisibleAsync(token);
            await dialog.ProfileNote.WaitVisibleAsync(token);
            await dialog.ShowAll.WaitVisibleAsync(token);
            await dialog.CancelButton.ClickAsync(token);
            await dialog.WaitHiddenAsync(token);
        }
    });
}
