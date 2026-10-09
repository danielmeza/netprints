using NetPrints.Desktop.E2ETests.Hosting;
using NetPrints.Testing.Ui.Dialogs;
using NetPrints.Testing.Ui.Driving;
using NetPrints.Testing.Ui.Graph;
using NetPrints.Testing.Ui.Shell;

namespace NetPrints.Desktop.E2ETests.Scenarios;

/// <summary>US9 scenario: the overloads button of Console.WriteLine opens the list with the current overload marked and first, a filtered pick changes the node's pins, and Undo restores it (FR-095).</summary>
public sealed class ChangeOverloadFlowTests(DesktopWorkerPool pool) : ProjectEditorTestBase(pool)
{
    private const string CallNode = "CallMethodNode";
    private const string Current = "void WriteLine(string value)";
    private const string Other = "void WriteLine(string format, object arg0)";
    private const string OtherPin = "in:arg0";

    [Fact]
    public Task PickingAnotherOverloadChangesThePinsAndUndoRestoresThem() => RunScenarioAsync(async token =>
    {
        await StartAsync(token);
        await WaitForProjectAsync(token);
        await Shell.OpenMethodAsync("Main", token);
        var node = Shell.Graph.Node(CallNode);
        var flyout = new OverloadFlyoutPage(Driver);
        IReadOnlyList<string> before = await CallNodePinsAsync(token);

        using (Step("open the overloads button and see the current overload first"))
        {
            await node.Overloads.ClickAsync(token);
            await flyout.WaitVisibleAsync(token);
            var rows = await flyout.RowsShownAsync(token);
            Assert.Equal(Current, rows[0].Text);
            Assert.Contains("current", rows[0].Classes, StringComparison.Ordinal);
            Assert.Single(rows, row => row.Classes.Contains("current", StringComparison.Ordinal));
            Assert.Contains(rows, row => row.Text == Other);
        }

        using (Step("filter, pick another overload, the pins change"))
        {
            await Driver.TypeAsync("(string format, object arg0)", token);
            await flyout.Row(Other).WaitVisibleAsync(token);
            await Driver.PressAsync("Enter", token);
            await flyout.WaitHiddenAsync(token);
            await WaitForPinsAsync(names => names.Contains(OtherPin), token);
            Assert.DoesNotContain(OtherPin, before);
        }

        using (Step("undo restores the previous overload"))
        {
            await Shell.Menu.InvokeAsync("Edit", ShellCommands.Undo, token);
            await WaitForPinsAsync(names => names.SequenceEqual(before), token);
        }
    });

    private async Task<IReadOnlyList<string>> CallNodePinsAsync(CancellationToken token)
    {
        var names = (await Shell.Graph.NodeNamesAsync(token)).Where(name => name.StartsWith(CallNode, StringComparison.Ordinal)).ToList();
        return names.Count == 1 ? await Shell.Graph.Node(names[0]).PinNamesAsync(token) : [];
    }

    private Task WaitForPinsAsync(Func<IReadOnlyList<string>, bool> condition, CancellationToken token) =>
        UiWait.UntilAsync(Driver, async () => condition(await CallNodePinsAsync(token)), "the call node's pins", token);
}
