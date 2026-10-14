using NetPrints.Desktop.E2ETests.Hosting;
using NetPrints.Editor;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Editor.Shell;
using NetPrints.Testing;
using NetPrints.Testing.Ui.Dialogs;
using NetPrints.Testing.Ui.Driving;
using NetPrints.Testing.Ui.Shell;
using NetPrints.Testing.Ui.Snapshots;

namespace NetPrints.Desktop.E2ETests.Scenarios;

/// <summary>
/// Produces the screenshots of the editor guides (FR-103, T102) from a private copy of the HelloWorld sample, with the
/// shipped defaults. It is explicit, so a normal run and CI's <c>--fail-skips on</c> job never run or skip it;
/// <c>scripts/guide-screenshots.sh</c> runs it with <c>NETPRINTS_GUIDE_SHOTS=1</c>. The project sits in a neutral folder
/// so no title bar, list or status line shows the machine's own paths.
/// </summary>
public sealed class GuideScreenshotTests(DesktopWorkerPool pool) : ProjectEditorTestBase(pool)
{
    /// <summary>The variable that opts in to writing the images.</summary>
    public const string EnableVariable = "NETPRINTS_GUIDE_SHOTS";

    private const string ImagesFolder = "website/static/img/guide/editor";
    private const string NeutralFolder = "NetPrints-Guide";
    private const int ShortBackupDelay = 300;
    private const int ImageMaxWidth = 1600;
    private const int ImageMaxHeight = 1000;
    private const double ScrollNotches = -15;

    private static readonly DocumentId MainDocument = DocumentId.Graph("HelloWorld.Program.netpc.json", DocumentId.MethodKeyPrefix + "m000000001gs20");

    protected override int? BackupDelayMilliseconds => ShortBackupDelay;

    private static string ImagesDirectory => Path.Combine(LocalSdkLayout.FindRepositoryRoot(), ImagesFolder);

    private static string GuideSample => Path.Combine(Path.GetTempPath(), NeutralFolder, "HelloWorld");

    private static string GuideProject => Path.Combine(GuideSample, "HelloWorld.csproj");

    private static string GuideClassFile => Path.Combine(GuideSample, "HelloWorld.Program.netpc.json");

    private string BackupsDirectory => Path.Combine(StateDirectory, "backups");

    protected override EditorStart EditorStartFor(string sampleProject)
    {
        string source = Path.GetDirectoryName(sampleProject) ?? "";
        string target = Path.GetDirectoryName(GuideSample) ?? "";
        if (Directory.Exists(target))
        {
            Directory.Delete(target, recursive: true);
        }

        Directory.CreateDirectory(GuideSample);
        foreach (string file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(GuideSample, Path.GetFileName(file)));
        }

        return new EditorStart(GuideProject, Environment(), WaitForProject: true);
    }

    [Fact(Explicit = true)]
    public Task TheGuidesAreIllustrated() => RunScenarioAsync(async token =>
    {
        Assert.SkipUnless(System.Environment.GetEnvironmentVariable(EnableVariable) == "1", $"Guide screenshots are written only with {EnableVariable}=1 (scripts/guide-screenshots.sh).");
        await StartAsync(token);
        await WaitForProjectAsync(token);

        using (Step("the shell with the main graph open"))
        {
            await Shell.OpenMethodAsync(MethodName, token);
            await Shell.Bottom.ShowAsync(PanelContributions.ErrorsId, token);
            await ShootAsync("shell-layout", token);
        }

        using (Step("a floated document, then Reset layout"))
        {
            await Shell.Menu.InvokeAsync("View", ShellCommands.FloatDocument, token);
            await UiWait.UntilAsync(Driver, async () => (await Shell.WindowTypesAsync(token)).Count == 2, "the floated window", token);
            var origin = (await Shell.GetAsync(token)).ScreenBounds;
            var floated = (await Shell.GraphOf(MainDocument).GetAsync(token)).Window;
            await Driver.MoveWindowAsync(floated, origin.X + 330, origin.Y + 160, token);
            await ShootAsync("shell-floating", token);
            await Shell.Menu.InvokeAsync("View", ShellCommands.ResetLayout, token);
            await UiWait.UntilAsync(Driver, async () => (await Shell.WindowTypesAsync(token)).Count == 1, "the floated window gone", token);
            await Shell.Inspector.WaitVisibleAsync(token);
            await Shell.Tabs.SelectAsync(MainDocument, token);
        }

        using (Step("the menus, the command bar, the palette and the shortcuts sheet"))
        {
            await Shell.Menu.OpenAsync("Edit", ShellCommands.AddVariable, token);
            await ShootAsync("menu-edit", token, parkPointer: false);
            await Driver.PressAsync("Escape", token);
            await Driver.SettleAsync(token);
            await ShootCommandBarAsync("command-bar", token);

            var palette = new CommandPalettePage(Driver);
            await Driver.PressAsync("Ctrl+Shift+P", token);
            await palette.WaitVisibleAsync(token);
            await Driver.TypeAsync("comp", token);
            await palette.QueryBox.WaitUntilAsync(e => e.Text == "comp", "the query typed", token);
            await palette.Row("Compile").WaitVisibleAsync(token);
            await ShootAsync("command-palette", token);
            await Driver.PressAsync("Escape", token);
            await palette.WaitHiddenAsync(token);

            await Shell.Menu.InvokeAsync("Help", ContributionIds.CommandPrefix + "keyboardShortcuts", token);
            var sheet = new KeyboardShortcutsDialogPage(Driver);
            await sheet.WaitVisibleAsync(token);
            await ShootAsync("keyboard-shortcuts", token);
            await sheet.CloseButton.ClickAsync(token);
            await sheet.WaitHiddenAsync(token);
        }

        string saved;
        using (Step("an unsaved change, the prompt, and a backup"))
        {
            await AddAVariableAsync(token);
            await Shell.Tree.SelectAsync(Shell.Tree.Method(MethodName), token);
            await Shell.Inspector.MethodInspector.WaitVisibleAsync(token);
            await Shell.WaitUntilAsync(e => e.Text?.Contains("HelloWorld*", StringComparison.Ordinal) == true, "the marker after the project name in the title", token);
            Assert.Equal("Main* – HelloWorld* – NetPrints", await Shell.TitleAsync(token));
            await ShootAsync("unsaved-marker", token);
            var prompt = new UnsavedChangesDialogPage(Driver);
            await Shell.CloseWindowAsync(token);
            await prompt.WaitVisibleAsync(token);
            await ShootAsync("unsaved-prompt", token, parkPointer: false);
            await prompt.CancelButton.ClickAsync(token);
            await prompt.WaitHiddenAsync(token);
            await WaitForAsync(() => Directory.Exists(BackupsDirectory) && Directory.EnumerateFiles(BackupsDirectory, "*.bak.json", SearchOption.AllDirectories).Any(), "the backup file", token);
            saved = await File.ReadAllTextAsync(GuideClassFile, token);
        }

        using (Step("the editor is killed and the recovery prompt shows"))
        {
            ExpectEditorExit();
            LeasedEditor.Kill();
            await LeasedEditor.Exited.WaitAsync(token);
            await RestartEditorAsync(new EditorStart(GuideProject, Environment(), WaitForProject: false), token);
            var recover = new RecoverDialogPage(Driver);
            await recover.WaitVisibleAsync(token);
            await ShootAsync("recover-prompt", token, parkPointer: false);
            await recover.RestoreButton.ClickAsync(token);
            await recover.WaitHiddenAsync(token);
            await WaitForProjectAsync(token);
            await Shell.Tree.RevealAsync(Shell.Tree.Variable("Variable"), ProjectTreePage.VariablesGroup, token);
        }

        var start = new StartPagePage(Driver);
        using (Step("the start page with a recent project"))
        {
            await Shell.Menu.InvokeAsync("File", ShellCommands.CloseProject, token);
            var prompt = new UnsavedChangesDialogPage(Driver);
            await prompt.WaitVisibleAsync(token);
            await prompt.DontSaveButton.ClickAsync(token);
            await Shell.Tree.Project("HelloWorld").WaitHiddenAsync(token);
            await start.WaitVisibleAsync(token);
            await start.WaitForRecentAsync(["HelloWorld"], token);
            await start.LearnGuide.WaitVisibleAsync(token);
            await ShootAsync("start-page", token);
        }

        using (Step("the New project dialog"))
        {
            await start.NewButton.ClickAsync(token);
            var dialog = new NewProjectDialogPage(Driver);
            await dialog.WaitVisibleAsync(token);
            await dialog.Templates.WaitVisibleAsync(token);
            await dialog.NameBox.ClickAsync(token);
            await Driver.TypeAsync("MyApp", token);
            await dialog.LocationBox.ClickAsync(token);
            await Driver.PressAsync("Ctrl+A", token);
            await Driver.TypeAsync(Path.GetDirectoryName(GuideSample) ?? "", token);
            await dialog.Preview.WaitVisibleAsync(token);
            await dialog.CreateButton.WaitUntilAsync(e => e.IsEnabled, "Create enabled", token);
            await dialog.LocationBox.WaitUntilAsync(e => e.Text == Path.GetDirectoryName(GuideSample), "the location typed", token);
            await ShootAsync("new-project-dialog", token, parkPointer: false);
            await dialog.CancelButton.ClickAsync(token);
            await dialog.WaitHiddenAsync(token);
        }

        using (Step("the what's new notes"))
        {
            await new UiElement(Driver, new AutomationQuery(AutomationIds.StartPageWhatsNewToggle)).ClickAsync(token);
            var notes = new UiElement(Driver, new AutomationQuery(AutomationIds.StartPageWhatsNew));
            await notes.WaitVisibleAsync(token);
            await Driver.WheelAsync(await start.CenterAsync(token), ScrollNotches, token);
            await Driver.SettleAsync(token);
            await ShootAsync("start-page-whats-new", token);
        }
    });

    /// <summary>Saves the shell window's area of the screen (its menus, dialogs and floated windows included) as a guide image.</summary>
    private async Task ShootAsync(string name, CancellationToken cancellationToken, bool parkPointer = true)
    {
        var bounds = (await Shell.GetAsync(cancellationToken)).ScreenBounds;
        await SaveAsync(name, bounds.X, bounds.Y, bounds.Width, bounds.Height, parkPointer, cancellationToken);
    }

    /// <summary>Saves the strip of the window that holds the menu bar and the command bar.</summary>
    private async Task ShootCommandBarAsync(string name, CancellationToken cancellationToken)
    {
        var bounds = (await Shell.GetAsync(cancellationToken)).ScreenBounds;
        var bar = (await Shell.Commands.GetAsync(cancellationToken)).ScreenBounds;
        await SaveAsync(name, bounds.X, bounds.Y, bounds.Width, bar.Y + bar.Height - bounds.Y, parkPointer: true, cancellationToken);
    }

    private async Task SaveAsync(string name, double x, double y, double width, double height, bool parkPointer, CancellationToken cancellationToken)
    {
        if (parkPointer)
        {
            await Driver.MoveAsync(await Shell.StatusMessage.CenterAsync(cancellationToken), cancellationToken);
        }

        // The first frame can predate the last repaint under software rendering, so the second one is kept.
        await Driver.SettleAsync(cancellationToken);
        await Driver.ScreenAsync(cancellationToken);
        await Driver.SettleAsync(cancellationToken);
        UiImage screen = await Driver.ScreenAsync(cancellationToken);
        UiImage image = screen.Crop((int)x, (int)y, (int)width, (int)height).Fit(ImageMaxWidth, ImageMaxHeight);
        image.Save(Path.Combine(ImagesDirectory, name + ".png"));
    }
}
