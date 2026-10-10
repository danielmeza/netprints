using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using NetPrints.Editor.UITests.Driving;
using NetPrints.Editor.UITests.Hosting;

namespace NetPrints.Editor.UITests.Shell;

/// <summary>The start page by keyboard: the search box has the focus, Down enters the list, Enter opens, Delete removes, Ctrl+P pins (FR-047).</summary>
public class StartPageKeyboardTests
{
    private const string Orbital = "/work/projects/Orbital/Orbital.csproj";
    private const string HelloWorld = "/work/projects/HelloWorld/HelloWorld.csproj";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string? FocusedId(StartPageRig rig) =>
        rig.Window.FocusManager?.GetFocusedElement() is Control control ? Avalonia.Automation.AutomationProperties.GetAutomationId(control) : null;

    private static Task PressAsync(StartPageRig rig, string chord) => rig.Ui.Driver.PressAsync(chord, Token);

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void TheSearchBoxHasTheFocusWhenThePageOpens()
    {
        using var rig = StartPageRig.Create(1600, 1000, withRecent: true);

        Assert.Equal(AutomationIds.StartPageRecentSearch, FocusedId(rig));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task DownMovesIntoTheListAndTheArrowsMoveThroughTheRows()
    {
        using var rig = StartPageRig.Create(1600, 1000, withRecent: true);

        await PressAsync(rig, "Down");
        Assert.Equal(Orbital, rig.Page.Recent?.SelectedItem?.Path);
        Assert.True(FocusedId(rig) != AutomationIds.StartPageRecentSearch, $"focus is on {rig.Window.FocusManager?.GetFocusedElement()?.GetType().Name} {FocusedId(rig)}");

        await PressAsync(rig, "Down");
        Assert.Equal(HelloWorld, rig.Page.Recent?.SelectedItem?.Path);

        await PressAsync(rig, "Up");
        Assert.Equal(Orbital, rig.Page.Recent?.SelectedItem?.Path);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task EnterOpensTheSelectedRow()
    {
        using var rig = StartPageRig.Create(1600, 1000, withRecent: true);

        await PressAsync(rig, "Down");
        await PressAsync(rig, "Down");
        await PressAsync(rig, "Enter");

        Assert.Equal([HelloWorld], rig.Actions.Opened);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task EnterOnAnUnavailableRowOpensNothing()
    {
        using var rig = StartPageRig.Create(1600, 1000, withRecent: true);

        for (int i = 0; i < 4; i++)
        {
            await PressAsync(rig, "Down");
        }

        Assert.Equal("Missing", rig.Page.Recent?.SelectedItem?.DisplayName);
        await PressAsync(rig, "Enter");
        Assert.Empty(rig.Actions.Opened);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task DeleteRemovesTheSelectedRowAndSelectsTheNextOne()
    {
        using var rig = StartPageRig.Create(1600, 1000, withRecent: true);

        await PressAsync(rig, "Down");
        await PressAsync(rig, "Delete");

        Assert.DoesNotContain(rig.Page.Recent?.Items ?? [], item => item.Path == Orbital);
        Assert.Equal(4, rig.Page.Recent?.Items.Count);
        Assert.Equal(HelloWorld, rig.Page.Recent?.SelectedItem?.Path);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task CtrlPPinsAnUnpinnedRowAndUnpinsAPinnedOne()
    {
        using var rig = StartPageRig.Create(1600, 1000, withRecent: true);

        await PressAsync(rig, "Down");
        await PressAsync(rig, "Down");
        await PressAsync(rig, "Ctrl+P");
        Assert.True(rig.Page.Recent?.Items.Single(item => item.Path == HelloWorld).Pinned);

        await PressAsync(rig, "Ctrl+P");
        Assert.False(rig.Page.Recent?.Items.Single(item => item.Path == HelloWorld).Pinned);
    }
}
