using Avalonia.Headless.XUnit;
using Dock.Model.Core;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Shell.Docking;
using NetPrints.Editor.UITests.Driving;
using NetPrints.Editor.UITests.Hosting;

namespace NetPrints.Editor.UITests.Shell;

/// <summary>Unloading a project after the user dragged, split and floated the tool panels (E-F9).</summary>
public class UnloadRearrangedPanelsTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task UnloadAsync(ShellApp app)
    {
        Assert.True(app.Commands.TryRun(app.Command("closeProject")));
        await Testing.Ui.Driving.UiWait.UntilAsync(app.Driver, () => Task.FromResult(app.Shell.Session is null), "project closed", Token);
        HeadlessDriver.Pump();
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task UnloadingAfterRearrangingThePanelsShowsTheStartPageAndTheNextLoadBringsThePanelsBack()
    {
        await using ShellApp app = ShellApp.Start(new MemoryStateStore());
        await app.OpenSampleAsync(Token);
        HeadlessDriver.Pump();
        var adapter = Assert.IsType<DockShellAdapter>(app.Api);
        RearrangedPanelsSuspensionTests.Rearrange(adapter);
        HeadlessDriver.Pump();

        await UnloadAsync(app);

        Assert.Null(app.Shell.Session);
        Assert.True(adapter.PanelsSuspended);
        Assert.All(app.Shell.Panels, panel => Assert.False(panel.IsVisible));
        Assert.Contains(DocumentId.StartPage, app.Api.OpenDocuments);

        await app.OpenSampleAsync(Token);
        HeadlessDriver.Pump();

        Assert.False(adapter.PanelsSuspended);
        Assert.All(app.Shell.Panels, panel => Assert.True(app.Api.IsPanelVisible(panel.Id)));
        List<string> ids = [.. ShellDockFactory.Walk(adapter.Layout).OfType<IDock>().Select(dock => dock.Id)];
        Assert.All(ids, id => Assert.False(string.IsNullOrEmpty(id)));
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }
}
