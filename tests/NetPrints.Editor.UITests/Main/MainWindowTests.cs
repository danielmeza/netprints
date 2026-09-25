using System.Diagnostics;
using Avalonia.Headless.XUnit;
using NetPrints.Editor.UITests.Hosting;
using NetPrints.Editor.Hosting.Automation;

namespace NetPrints.Editor.UITests.Main;

public class MainWindowTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task StartsAndOpensStartupProject()
    {
        using var sample = new SampleCopy();
        var sw = Stopwatch.StartNew();
        using var app = HeadlessApp.Start();
        var frame = await app.Driver.ScreenshotAsync((await app.Main.GetAsync(Token)).Window, Token);
        sw.Stop();

        Assert.True(frame.Width > 0);
        Assert.True(sw.ElapsedMilliseconds < 5000, $"SC-006: main window in {sw.ElapsedMilliseconds} ms");
        Assert.Equal(800, await app.Main.GetAsync<double>(AutomationPropertyNames.Width, Token));
        Assert.Equal(600, await app.Main.GetAsync<double>(AutomationPropertyNames.Height, Token));
        Assert.Equal("NetPrints", await app.Main.TitleAsync(Token));

        await app.OpenStartupProjectAsync(sample.ProjectPath, Token);

        Assert.Equal("HelloWorld", await app.Main.TitleAsync(Token)); // PAR-01
        Assert.Equal(["HelloWorld.Program"], await app.Main.ClassNamesAsync(Token)); // PAR-05, PAR-11
        Assert.True(app.Dialogs.Errors.Count == 0, string.Join("\n---\n", app.Dialogs.Errors.Select(e => e.Message)));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task ProjectAndSettingsPanesAndEnablement()
    {
        using var sample = new SampleCopy();
        using var app = HeadlessApp.Start();
        var main = app.Main;

        Assert.False(await main.SettingsButton.IsEnabledAsync(Token)); // PAR-07
        Assert.False(await main.ReferencesButton.IsEnabledAsync(Token)); // PAR-08
        Assert.False(await main.CompileButton.IsEnabledAsync(Token));
        Assert.Equal("True", await main.CompileButton.PropertyAsync(AutomationPropertyNames.ShowToolTipOnDisabled, Token)); // PAR-09

        await main.ProjectButton.ClickAsync(Token);
        Assert.True(await main.ProjectPane.IsVisibleAsync(Token));
        Assert.False(await main.SaveProjectButton.IsEnabledAsync(Token)); // PAR-02

        await app.OpenStartupProjectAsync(sample.ProjectPath, Token);
        Assert.True(await main.SaveProjectButton.IsEnabledAsync(Token));
        Assert.True(await main.CompileButton.IsEnabledAsync(Token));
        Assert.True(await main.RunButton.IsEnabledAsync(Token));

        // With the Project pane still open, one click on Settings switches panes (PAR-02).
        Assert.True(await main.ProjectPane.IsVisibleAsync(Token));
        await main.SettingsButton.ClickAsync(Token);
        Assert.True(await main.SettingsPane.IsVisibleAsync(Token));
        Assert.False(await main.ProjectPane.IsVisibleAsync(Token));

        await main.ProjectButton.ClickAsync(Token);
        Assert.True(await main.ProjectPane.IsVisibleAsync(Token));
        Assert.False(await main.SettingsPane.IsVisibleAsync(Token));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task ToolbarLabelsFitTheirButtons()
    {
        using var app = HeadlessApp.Start();

        foreach (var button in app.Main.ToolbarButtons)
        {
            var element = await button.GetAsync(Token);
            Assert.True(element[AutomationPropertyNames.TextOverflows] == "False", $"'{element.Text}' does not fit its {element[AutomationPropertyNames.Width]}-DIP round button");
        }
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task OpenProjectThroughTheFilePicker()
    {
        using var sample = new SampleCopy();
        using var app = HeadlessApp.Start();
        await app.Main.ShowProjectPaneAsync(Token);

        app.FilePicker.Enqueue("open", "Open Project", sample.ProjectPath);
        await app.Main.OpenProjectButton.ClickAsync(Token); // PAR-03

        await app.Main.WaitForProjectAsync("HelloWorld", Token);
        Assert.Equal(["open:Open Project"], app.FilePicker.Requests);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task ClassWindowIsReusedAndRestored()
    {
        using var sample = new SampleCopy();
        using var app = HeadlessApp.Start();
        await app.OpenStartupProjectAsync(sample.ProjectPath, Token);
        var page = await app.Main.OpenClassAsync("HelloWorld.Program", Token);
        Assert.Equal("Maximized", await page.WindowStateAsync(Token)); // PAR-22
        var window = app.ClassWindow("HelloWorld.Program");

        window.WindowState = Avalonia.Controls.WindowState.Minimized; // what the window manager would do
        await app.Main.ClassButton("HelloWorld.Program").ClickAsync(Token); // PAR-14: activate, do not open another

        Assert.Single(app.Composition.Windows.ClassEditorWindows);
        Assert.Same(window, app.ClassWindow("HelloWorld.Program"));
        Assert.Equal("Normal", await page.WindowStateAsync(Token));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task RemovingAClassClosesItsWindow()
    {
        using var sample = new SampleCopy();
        using var app = HeadlessApp.Start();
        await app.OpenStartupProjectAsync(sample.ProjectPath, Token);
        var page = await app.Main.OpenClassAsync("HelloWorld.Program", Token);

        await app.Main.RemoveClassButton("HelloWorld.Program").ClickAsync(Token); // PAR-11

        Assert.False(await page.ExistsAsync(Token));
        Assert.Empty(await app.Main.ClassNamesAsync(Token));
    }
}
