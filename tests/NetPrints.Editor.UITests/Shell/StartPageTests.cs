using Avalonia.Headless.XUnit;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Editor.UITests.Driving;
using NetPrints.Testing.Ui.Driving;

namespace NetPrints.Editor.UITests.Shell;

/// <summary>The start page in the shell window (FR-040, FR-043, FR-044): its tiles render, and the startup error shows.</summary>
public class StartPageTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static UiElement Element(ShellApp app, string automationId) => new(app.Driver, new AutomationQuery(automationId));

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task WithNoProjectTheStartPageShowsItsTiles()
    {
        await using ShellApp app = ShellApp.Start();
        HeadlessDriver.Pump();

        Assert.True(await Element(app, AutomationIds.StartPageRoot).ExistsAsync(Token));
        Assert.True(await Element(app, AutomationIds.StartPageRecentSearch).ExistsAsync(Token));
        Assert.True(await Element(app, AutomationIds.StartPageOpenButton).ExistsAsync(Token));
        Assert.True(await Element(app, AutomationIds.StartPageNewButton).ExistsAsync(Token));
        Assert.True(await Element(app, AutomationIds.StartPageSampleOpen).ExistsAsync(Token));
        Assert.True(await Element(app, AutomationIds.StartPageReleasesLink).ExistsAsync(Token));
        Assert.False(await Element(app, AutomationIds.StartPageError).IsVisibleAsync(Token));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task AStartupErrorShowsAboveTheTilesNamingThePath()
    {
        await using ShellApp app = ShellApp.Start();
        app.Shell.StartPageError = "'/nowhere/App.csproj' does not exist.";
        HeadlessDriver.Pump();

        UiElement error = Element(app, AutomationIds.StartPageError);
        Assert.True(await error.IsVisibleAsync(Token));
        Assert.Contains("/nowhere/App.csproj", await error.TextAsync(Token), StringComparison.Ordinal);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task OpeningAProjectClosesTheStartPage()
    {
        await using ShellApp app = ShellApp.Start();
        Assert.Equal([NetPrints.Editor.Shell.DocumentId.StartPage], app.Api.OpenDocuments);

        await app.OpenSampleAsync(Token);

        Assert.Empty(app.Api.OpenDocuments);
        Assert.False(await Element(app, AutomationIds.StartPageRoot).ExistsAsync(Token));
    }
}
