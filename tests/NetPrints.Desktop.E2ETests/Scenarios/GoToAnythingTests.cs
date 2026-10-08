using NetPrints.Desktop.E2ETests.Hosting;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Testing.Ui.Dialogs;
using NetPrints.Testing.Ui.Driving;

namespace NetPrints.Desktop.E2ETests.Scenarios;

/// <summary>Go to anything, connection jumps, history and the connection tooltip (FR-061, FR-062, FR-063, US7).</summary>
public sealed class GoToAnythingTests(DesktopWorkerPool pool) : ProjectEditorTestBase(pool)
{
    private const string MainMethod = "Main";
    private const string EntryNode = "MethodEntryNode";
    private const string CallNode = "CallMethodNode";
    private const string WriteLine = "WriteLine";

    [Fact]
    public Task FindANodeJumpAlongACableGoBackAndReadTheTooltip() => RunScenarioAsync(async token =>
    {
        await StartAsync(token);
        await WaitForProjectAsync(token);
        var graph = Shell.Graph;
        var driver = Driver;
        await Shell.OpenMethodAsync(MainMethod, token);
        var viewport = await graph.ViewportAsync(token);
        string cable = (await graph.ConnectionNamesAsync(token)).Single(name => name.StartsWith(EntryNode + ".", StringComparison.Ordinal));

        using (Step("the connection tooltip"))
        {
            await graph.Connection(cable).HoverAsync(token);
            await graph.Connection(cable).WaitUntilAsync(e => e[AutomationPropertyNames.ToolTipIsOpen] == bool.TrueString, "the tooltip open", token);
            string tip = await graph.Connection(cable).ToolTipAsync(token) ?? "";
            Assert.StartsWith(cable.Replace("->", " → ", StringComparison.Ordinal), tip, StringComparison.Ordinal);
        }

        using (Step("Ctrl+click on a connection reaches its other end"))
        {
            await graph.Connection(cable).CtrlClickAtAsync(0.3, token);
            await graph.Node(CallNode).WaitUntilAsync(e => e[AutomationPropertyNames.IsSelected] == bool.TrueString, "the far end selected", token);
        }

        using (Step("Alt+Left restores the same view"))
        {
            await driver.PressAsync("Alt+Left", token);
            await graph.WaitForViewportAsync(viewport, token);
        }

        using (Step("go to a node by its title, then back"))
        {
            await driver.PressAsync("Ctrl+P", token);
            var goTo = new GoToAnythingPage(driver);
            await goTo.WaitVisibleAsync(token);
            await driver.TypeAsync(WriteLine, token);
            await goTo.WaitForRowContainingAsync(WriteLine, token);
            await driver.PressAsync("Enter", token);
            await goTo.WaitHiddenAsync(token);
            await graph.WaitForGraphAsync(MainMethod, token);
            await UiWait.UntilAsync(driver, async () => await graph.ViewportAsync(token) != viewport, "the view moved to the node", token);

            await driver.PressAsync("Alt+Left", token);
            await graph.WaitForViewportAsync(viewport, token);
        }
    });
}
