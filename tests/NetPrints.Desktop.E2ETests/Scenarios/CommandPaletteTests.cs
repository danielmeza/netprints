using NetPrints.Desktop.E2ETests.Hosting;
using NetPrints.Testing.Ui.Dialogs;
using NetPrints.Testing.Ui.Driving;

namespace NetPrints.Desktop.E2ETests.Scenarios;

/// <summary>The command palette opens with Ctrl+Shift+P, filters by what is typed and runs the chosen command (FR-060, US7).</summary>
public sealed class CommandPaletteTests(DesktopWorkerPool pool) : ProjectEditorTestBase(pool)
{
    [Fact]
    public Task TypingCompAndEnterCompilesTheProject() => RunScenarioAsync(async token =>
    {
        await StartAsync(token);
        await WaitForProjectAsync(token);
        var driver = Driver;
        var palette = new CommandPalettePage(driver);

        using (Step("open the palette and filter"))
        {
            await driver.PressAsync("Ctrl+Shift+P", token);
            await palette.WaitVisibleAsync(token);
            await driver.TypeAsync("comp", token);
            await palette.Row("Compile").WaitVisibleAsync(token);
        }

        using (Step("run the first match"))
        {
            await driver.PressAsync("Enter", token);
            await palette.WaitHiddenAsync(token);
            await Shell.StatusMessage.WaitUntilAsync(e => e.Text == "Build succeeded", "Build succeeded", token, TimeSpan.FromSeconds(120));
        }
    });
}
