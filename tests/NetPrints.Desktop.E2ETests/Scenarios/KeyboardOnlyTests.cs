using NetPrints.Desktop.E2ETests.Hosting;
using NetPrints.Testing.Ui.Dialogs;
using NetPrints.Testing.Ui.Driving;
using NetPrints.Testing.Ui.Shell;

namespace NetPrints.Desktop.E2ETests.Scenarios;

/// <summary>Compile, run, stop, switch and close tabs, edit, undo, redo and save with the keyboard alone (SC-003, SC-004). The graphs are opened with the pointer first.</summary>
public sealed class KeyboardOnlyTests(DesktopWorkerPool pool) : ProjectEditorTestBase(pool)
{
    private const string MainMethod = "Main";
    private const string ClassGraph = "Program";
    private const string DebugWriteLine = "System.Diagnostics.Debug WriteLine (value: System.Object)";
    private const string HoldingSource = """
        using System;
        using System.Runtime.CompilerServices;
        using System.Threading;

        namespace HelloWorld;

        internal static class Hold
        {
            [ModuleInitializer]
            internal static void Start()
            {
                Console.WriteLine("Holding");
                Thread.Sleep(TimeSpan.FromMinutes(5));
            }
        }
        """;

    [Fact]
    public Task EveryCommonActionWorksWithoutTheMouse() => RunScenarioAsync(async token =>
    {
        await StartAsync(token);
        await WaitForProjectAsync(token);
        await File.WriteAllTextAsync(Path.Combine(SampleDirectory, "Hold.cs"), HoldingSource, token);
        var graph = Shell.Graph;
        var driver = Driver;

        using (Step("open two graphs"))
        {
            await Shell.Tree.SelectAsync(Shell.Tree.Class(ClassGraph), token);
            await driver.PressAsync("Enter", token);
            await graph.WaitForGraphAsync(ClassGraph, token);
            await Shell.OpenMethodAsync(MainMethod, token);
        }

        using (Step("add a node, undo, redo and save"))
        {
            byte[] saved = await File.ReadAllBytesAsync(ClassFile, token);
            int nodes = await graph.NodeCountAsync(token);
            await driver.PressAsync("Ctrl+Space", token);
            var search = await graph.Search.WaitOpenAsync(token);
            await search.FilterAsync("debug write line", DebugWriteLine, token);
            await search.PressEnterAsync(token);
            await search.WaitClosedAsync(token);
            await UiWait.UntilAsync(driver, async () => await graph.NodeCountAsync(token) == nodes + 1, "the node added", token);

            await driver.PressAsync("Ctrl+Z", token);
            await UiWait.UntilAsync(driver, async () => await graph.NodeCountAsync(token) == nodes, "the node undone", token);
            await driver.PressAsync("Ctrl+Y", token);
            await UiWait.UntilAsync(driver, async () => await graph.NodeCountAsync(token) == nodes + 1, "the node redone", token);

            await driver.PressAsync("Ctrl+S", token);
            await WaitForAsync(() => !saved.AsSpan().SequenceEqual(File.ReadAllBytes(ClassFile)), "the saved file", token);
        }

        using (Step("compile"))
        {
            await driver.PressAsync("F7", token);
            await Shell.StatusMessage.WaitUntilAsync(e => e.Text == "Build succeeded", "Build succeeded", token, TimeSpan.FromSeconds(120));
        }

        using (Step("run and stop"))
        {
            await driver.PressAsync("F5", token);
            await Shell.StatusMessage.WaitUntilAsync(e => e.Text == "Running…", "Running", token, TimeSpan.FromSeconds(120));
            await driver.PressAsync("Shift+F5", token);
            await Shell.StatusMessage.WaitUntilAsync(e => e.Text?.StartsWith("Exited with code", StringComparison.Ordinal) == true, "Exited", token);
        }

        using (Step("find a node and go back"))
        {
            var viewport = await graph.ViewportAsync(token);
            await driver.PressAsync("Ctrl+P", token);
            var goTo = new GoToAnythingPage(driver);
            await goTo.WaitVisibleAsync(token);
            await driver.TypeAsync("WriteLine", token);
            await goTo.WaitForRowContainingAsync("WriteLine", token);
            await driver.PressAsync("Enter", token);
            await goTo.WaitHiddenAsync(token);

            await driver.PressAsync("Alt+Left", token);
            await graph.WaitForViewportAsync(viewport, token);
        }

        using (Step("switch tabs"))
        {
            await driver.PressAsync("Ctrl+Tab", token);
            await graph.WaitForGraphAsync(ClassGraph, token);
        }

        using (Step("close the tab"))
        {
            await driver.PressAsync("Ctrl+W", token);
            await graph.WaitForGraphAsync(MainMethod, token);
        }
    });
}
