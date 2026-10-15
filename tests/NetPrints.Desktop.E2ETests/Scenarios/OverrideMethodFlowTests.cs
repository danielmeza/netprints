using NetPrints.Desktop.E2ETests.Hosting;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Testing.Ui.Dialogs;
using NetPrints.Testing.Ui.Shell;

namespace NetPrints.Desktop.E2ETests.Scenarios;

/// <summary>US9 scenario: Edit > Override method... lists the base methods, Enter picks the filtered one, its graph opens, and the dialog dims it afterwards (FR-094, FR-095).</summary>
public sealed class OverrideMethodFlowTests(DesktopWorkerPool pool) : ProjectEditorTestBase(pool)
{
    private const string ToStringRow = "string ToString()";

    [Fact]
    public Task FilteringAndPressingEnterOverridesTheMethodAndTheDialogDimsItAfterwards() => RunScenarioAsync(async token =>
    {
        await StartAsync(token);
        await WaitForProjectAsync(token);
        var tree = Shell.Tree;
        var dialog = new SelectMethodDialogPage(Driver);

        using (Step("open the override dialog on the class"))
        {
            await tree.SelectAsync(tree.Class("Program"), token);
            await Shell.Menu.InvokeAsync("Edit", ShellCommands.OverrideMethod, token, scrollOverCommandId: ShellCommands.AddVariable);
            await dialog.WaitVisibleAsync(token);
            await dialog.Picker.Row(ToStringRow).WaitVisibleAsync(token);
        }

        using (Step("type part of the name and press Enter"))
        {
            await Driver.TypeAsync("tostr", token);
            await Driver.PressAsync("Enter", token);
            await dialog.WaitHiddenAsync(token);
        }

        using (Step("the override's graph opens and the project tree lists it"))
        {
            await Shell.Graph.WaitForGraphAsync("ToString", token);
            await tree.RevealAsync(tree.Method("ToString"), ProjectTreePage.MethodsGroup, token);
        }

        using (Step("reopening the dialog shows the method dimmed"))
        {
            await tree.SelectAsync(tree.Class("Program"), token);
            await Shell.Menu.InvokeAsync("Edit", ShellCommands.OverrideMethod, token, scrollOverCommandId: ShellCommands.AddVariable);
            await dialog.WaitVisibleAsync(token);
            await dialog.Picker.Row(ToStringRow).WaitUntilAsync(e => e[AutomationPropertyNames.PseudoClasses]?.Contains("dimmed", StringComparison.Ordinal) == true, "the row dimmed", token);
        }
    });
}
