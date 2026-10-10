using Avalonia.Headless.XUnit;
using NetPrints.Editor.UITests.Driving;

namespace NetPrints.Editor.UITests.Shell;

/// <summary>
/// What a test leaves open goes with the test: an open window stays registered with its dispatcher's render loop, and
/// static caches of Avalonia controls keep that dispatcher, so every window left open keeps its whole editor alive for the run.
/// </summary>
public class ShellAppReleaseTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task DisposingTheAppClosesTheShellWindowOfAProjectWithUnsavedChanges()
    {
        ShellApp app = ShellApp.Start();
        await app.OpenSampleAsync(Token);
        app.Session.ContextFor(app.Session.Project.Classes[0]).CreateVariable();
        Assert.True(app.Session.Project.Classes[0].IsDirty);

        await app.DisposeAsync();
        HeadlessDriver.Pump();

        Assert.False(app.Window.IsVisible);
    }
}
