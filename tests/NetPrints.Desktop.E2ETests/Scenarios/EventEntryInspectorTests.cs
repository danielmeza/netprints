using System.Text.RegularExpressions;
using NetPrints.Desktop.E2ETests.Hosting;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Testing.Ui.Dialogs;
using NetPrints.Testing.Ui.Driving;
using NetPrints.Testing.Ui.Shell;

namespace NetPrints.Desktop.E2ETests.Scenarios;

/// <summary>SC-009: a custom event renamed and given two arguments in the inspector produces the renamed method with both parameters, and each change undoes in one step.</summary>
public sealed class EventEntryInspectorTests(DesktopWorkerPool pool) : ProjectEditorTestBase(pool)
{
    private const string EventName = "OnHit";

    [Fact]
    public Task ARenamedCustomEventWithTwoArgumentsIsInTheGeneratedCodeAndUndoesStepByStep() => RunScenarioAsync(async token =>
    {
        await StartAsync(token);
        await WaitForProjectAsync(token);
        var graph = Shell.Graph;
        var inspector = Shell.Inspector;
        var driver = Driver;

        using (Step("add an event graph with a custom event"))
        {
            await Shell.Tree.SelectAsync(Shell.Tree.Class("Program"), token);
            var palette = new CommandPalettePage(driver);
            await driver.PressAsync("Ctrl+Shift+P", token);
            await palette.WaitVisibleAsync(token);
            await driver.TypeAsync("add event graph", token);
            await palette.Row("Add event graph").WaitVisibleAsync(token);
            await driver.PressAsync("Enter", token);
            await palette.WaitHiddenAsync(token);
            await graph.Watermark.WaitUntilAsync(e => !string.IsNullOrEmpty(e.Text), "the event graph shown", token);
            var search = await (await graph.RightClickEmptyAsync(token)).WaitOpenAsync(token);
            await search.FilterAsync("Custom Event", "Custom Event", token);
            await search.ChooseAsync("Custom Event", token);
            await UiWait.UntilAsync(driver, async () => await graph.NodeCountAsync(token) == 1, "the entry added", token);
            await graph.Node((await graph.NodeNamesAsync(token))[0]).SelectAsync(token);
            await inspector.EventEntryInspector.WaitVisibleAsync(token);
        }

        string original = await inspector.EventEntryName.TextAsync(token) ?? "";

        using (Step("rename the event"))
        {
            await inspector.EventEntryName.ClickAsync(token);
            await driver.PressAsync("Ctrl+A", token);
            await driver.TypeAsync(EventName, token);
            await driver.PressAsync("Tab", token);
            await inspector.EventEntryName.WaitUntilAsync(e => e.Text == EventName, "the new name kept", token);
        }

        using (Step("add two arguments"))
        {
            await inspector.EventEntryAddArgument.ClickAsync(token);
            await UiWait.UntilAsync(driver, async () => await inspector.EventEntryArgumentCountAsync(token) == 1, "one argument", token);
            await inspector.EventEntryAddArgument.ClickAsync(token);
            await UiWait.UntilAsync(driver, async () => await inspector.EventEntryArgumentCountAsync(token) == 2, "two arguments", token);
        }

        using (Step("compile and read the generated C#"))
        {
            await driver.PressAsync("F7", token);
            await Shell.StatusMessage.WaitUntilAsync(e => e.Text == "Build succeeded", "Build succeeded", token, TimeSpan.FromSeconds(120));
            await Shell.Bottom.ShowAsync(PanelContributions.CSharpId, token);
            await Shell.Bottom.CSharpCode.WaitUntilAsync(
                e => Regex.IsMatch(e.Text ?? "", @"\b" + EventName + @"\([^,()]+,[^,()]+\)"), "the method with both parameters", token);
        }

        using (Step("each change undoes in one step"))
        {
            await Shell.Menu.InvokeAsync("Edit", ShellCommands.Undo, token);
            await UiWait.UntilAsync(driver, async () => await inspector.EventEntryArgumentCountAsync(token) == 1, "one argument again", token);
            await Shell.Menu.InvokeAsync("Edit", ShellCommands.Undo, token);
            await UiWait.UntilAsync(driver, async () => await inspector.EventEntryArgumentCountAsync(token) == 0, "no arguments again", token);
            Assert.Equal(EventName, await inspector.EventEntryName.TextAsync(token));
            await Shell.Menu.InvokeAsync("Edit", ShellCommands.Undo, token);
            await inspector.EventEntryName.WaitUntilAsync(e => e.Text == original, "the first name back", token);
        }
    });
}
