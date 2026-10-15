using NetPrints.Desktop.E2ETests.Hosting;
using NetPrints.Editor;
using NetPrints.Testing.Ui.Dialogs;
using NetPrints.Testing.Ui.Driving;
using NetPrints.Testing.Ui.Shell;

namespace NetPrints.Desktop.E2ETests.Scenarios;

/// <summary>US8 scenario 1: F2 on an event graph row renames it in place from the keyboard, and the rename undoes in one step.</summary>
public sealed class TreeInlineRenameTests(DesktopWorkerPool pool) : ProjectEditorTestBase(pool)
{
    private const string NewName = "Gameplay";

    [Fact]
    public Task F2OnAnEventGraphRowRenamesItInPlaceAndUndoesInOneStep() => RunScenarioAsync(async token =>
    {
        await StartAsync(token);
        await WaitForProjectAsync(token);
        var tree = Shell.Tree;
        var driver = Driver;

        using (Step("add an event graph and select its row"))
        {
            await tree.SelectAsync(tree.Class("Program"), token);
            var palette = new CommandPalettePage(driver);
            await driver.PressAsync("Ctrl+Shift+P", token);
            await palette.WaitVisibleAsync(token);
            await driver.TypeAsync("add event graph", token);
            await palette.Row("Add event graph").WaitVisibleAsync(token);
            await driver.PressAsync("Enter", token);
            await palette.WaitHiddenAsync(token);
            var row = await tree.RevealAsync(tree.Item(AutomationIds.TreeKindEventGraph, "EventGraph"), ProjectTreePage.EventGraphsGroup, token);
            await tree.SelectAsync(row, token);
        }

        using (Step("F2, type the new name, Enter"))
        {
            await driver.PressAsync("F2", token);
            await tree.RenameBox(AutomationIds.TreeKindEventGraph, "EventGraph").WaitVisibleAsync(token);
            await driver.TypeAsync(NewName, token);
            await driver.PressAsync("Enter", token);
            await tree.Item(AutomationIds.TreeKindEventGraph, NewName).WaitVisibleAsync(token);
        }

        using (Step("undo restores the name in one step"))
        {
            await Shell.Menu.InvokeAsync("Edit", ShellCommands.Undo, token);
            await tree.Item(AutomationIds.TreeKindEventGraph, "EventGraph").WaitVisibleAsync(token);
        }
    });
}
