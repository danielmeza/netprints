using NetPrints.Desktop.E2ETests.Hosting;
using NetPrints.Testing;
using NetPrints.Testing.Ui.Dialogs;
using NetPrints.Testing.Ui.Driving;
using NetPrints.Testing.Ui.Shell;

namespace NetPrints.Desktop.E2ETests.Scenarios;

/// <summary>The editor starts on the start page: a new Console project in an empty folder opens, lists in Recent, reopens from it, pins, is found by search and is removed (FR-040 to FR-042, US5).</summary>
public sealed class StartPageNewProjectTests(DesktopWorkerPool pool) : ProjectEditorTestBase(pool)
{
    private const string ProjectName = "Demo";

    protected override EditorStart EditorStartFor(string sampleProject)
    {
        UseTheRepositorySdk();
        return new(null, Environment(), WaitForProject: false);
    }

    /// <summary>The SDK package a template references is not published for this repository's builds: a project created under the private work folder builds against the in-repo SDK and drops the package reference, as the samples do.</summary>
    private void UseTheRepositorySdk()
    {
        LocalSdkLayout.Write(Work);
        string targets = Path.Combine(Work, "Directory.Build.targets");
        File.WriteAllText(targets, File.ReadAllText(targets).Replace("</Project>",
            "  <ItemGroup>\n    <PackageReference Remove=\"NetPrints.Sdk\" />\n  </ItemGroup>\n</Project>", StringComparison.Ordinal));
    }

    [Fact]
    public Task ANewProjectIsCreatedListedReopenedPinnedFoundAndRemoved() => RunScenarioAsync(async token =>
    {
        await StartAsync(token);
        var start = new StartPagePage(Driver);
        string folder = Path.Combine(Work, ProjectName);
        string project = Path.Combine(folder, ProjectName + ".csproj");

        using (Step("the start page shows with no recent project"))
        {
            await start.WaitVisibleAsync(token);
            await start.Empty.WaitVisibleAsync(token);
            Assert.Empty(await start.RecentNamesAsync(token));
        }

        using (Step("create a Console project in an empty folder"))
        {
            await start.NewButton.ClickAsync(token);
            var dialog = new NewProjectDialogPage(Driver);
            await dialog.WaitVisibleAsync(token);
            await dialog.CreateAsync(ProjectName, Work, token);
            await Shell.WaitForProjectAsync(ProjectName, token);
            await Shell.Tree.Class("Program").WaitVisibleAsync(token);
            await start.WaitHiddenAsync(token);
            Assert.True(File.Exists(project));
            Assert.True(File.Exists(Path.Combine(folder, "Program.netpc.json")));
        }

        using (Step("close it and find it in Recent"))
        {
            await Shell.Menu.InvokeAsync("File", ShellCommands.CloseProject, token);
            await start.WaitVisibleAsync(token);
            await start.WaitForRecentAsync([ProjectName], token);
        }

        using (Step("reopen it from Recent"))
        {
            await start.Recent(ProjectName).Open.ClickAsync(token);
            await Shell.WaitForProjectAsync(ProjectName, token);
            await Shell.Menu.InvokeAsync("File", ShellCommands.CloseProject, token);
            await start.WaitVisibleAsync(token);
            await start.WaitForRecentAsync([ProjectName], token);
        }

        using (Step("pin it"))
        {
            var row = start.Recent(ProjectName);
            await row.Pin.ClickAsync(token);
            await row.Unpin.WaitVisibleAsync(token);
            await row.Pin.WaitHiddenAsync(token);
        }

        using (Step("search for it"))
        {
            await start.SearchForAsync("emo", token);
            await start.WaitForRecentAsync([ProjectName], token);
            await start.SearchForAsync("no-such-project", token);
            await start.WaitForRecentAsync([], token);
            await start.Empty.WaitVisibleAsync(token);
            await start.SearchForAsync("", token);
            await start.WaitForRecentAsync([ProjectName], token);
        }

        using (Step("remove it"))
        {
            await start.Recent(ProjectName).Remove.ClickAsync(token);
            await start.WaitForRecentAsync([], token);
            await start.Empty.WaitVisibleAsync(token);
            Assert.True(File.Exists(project), "removing from the list keeps the project");
        }
    });
}
