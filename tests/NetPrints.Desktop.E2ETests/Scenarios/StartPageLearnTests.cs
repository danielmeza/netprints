using NetPrints.Desktop.E2ETests.Hosting;
using NetPrints.Testing.Ui.Dialogs;
using NetPrints.Testing.Ui.Driving;
using NetPrints.Testing.Ui.Shell;

namespace NetPrints.Desktop.E2ETests.Scenarios;

/// <summary>The start page's Learn card links the guide, the documentation and the release notes, and opens the keyboard shortcuts sheet (FR-049, US5).</summary>
public sealed class StartPageLearnTests(DesktopWorkerPool pool) : ProjectEditorTestBase(pool)
{
    protected override EditorStart EditorStartFor(string sampleProject) => new(null, Environment(), WaitForProject: false);

    [Fact]
    public Task TheLearnCardLinksAndTheShortcutsSheetOpensFromIt() => RunScenarioAsync(async token =>
    {
        await StartAsync(token);
        var start = new StartPagePage(Driver);

        using (Step("the start page shows the Learn card links and the startup check box"))
        {
            await start.WaitVisibleAsync(token);
            foreach (UiElement link in new[] { start.LearnGuide, start.LearnShortcuts, start.LearnDocs, start.LearnReleaseNotes, start.ReopenLast })
            {
                await link.WaitVisibleAsync(token);
            }
        }

        using (Step("the keyboard shortcuts link opens the sheet, which closes again"))
        {
            await start.LearnShortcuts.ClickAsync(token);
            var shortcuts = new KeyboardShortcutsDialogPage(Driver);
            await shortcuts.WaitVisibleAsync(token);
            await shortcuts.CloseButton.ClickAsync(token);
            await shortcuts.WaitHiddenAsync(token);
            await start.WaitVisibleAsync(token);
        }
    });
}
