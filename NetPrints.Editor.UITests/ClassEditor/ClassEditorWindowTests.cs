using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Editor.ClassEditor;

namespace NetPrints.Editor.UITests.ClassEditor;

public class ClassEditorWindowTests
{
    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task InspectorsListsAndGeneratedCode()
    {
        using var session = await EditorSession.OpenSampleMainAsync();
        var page = session.ClassEditor;

        Assert.Equal(5, page.SplitterCount); // PAR-31
        Assert.True(ToolTip.GetShowOnDisabled(page.CompileButton));

        // Opening Main by double click: the first click already showed the method inspector (PAR-24, 33).
        Assert.True(page.IsMethodInspectorVisible);
        Assert.Equal(InspectorKind.Method, page.ViewModel.Inspector);
        Assert.False(page.IsClassInspectorVisible);

        page.ViewModel.CreateVariableCommand.Execute(null); // PAR-29, 30
        HeadlessInput.Pump();
        page.ClickVariable("Variable");
        await HeadlessInput.WaitUntilAsync(() => page.IsVariableInspectorVisible, "variable inspector");

        page.ClickClassButton(); // PAR-23, 34
        Assert.Same(page.ViewModel.Class, page.ViewModel.OpenedGraph?.Graph);
        await HeadlessInput.WaitUntilAsync(() => page.IsClassInspectorVisible, "class inspector");
        page.ClassNameBox.Text = "Renamed";
        HeadlessInput.Pump();
        Assert.Equal("Renamed", page.ViewModel.Class.Name); // updates as typed
        Assert.Equal("Renamed", page.Window.Title);
        await HeadlessInput.WaitUntilAsync(() => (page.GeneratedCodeBox.Text ?? "").Contains("class Renamed"), "generated code refreshed", 5000);
        Assert.True(page.GeneratedCodeBox.IsReadOnly);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task OverrideChooserCreatesAndResets()
    {
        using var session = await EditorSession.OpenSampleMainAsync();
        var page = session.ClassEditor;
        var toString = page.ViewModel.OverridableMethods.First(m => m.Name == "ToString"); // PAR-26

        page.OverrideChooser.SelectedItem = toString;
        await HeadlessInput.WaitUntilAsync(() => page.OverrideChooser.SelectedItem is null, "chooser reset");

        Assert.Contains(page.ViewModel.Methods, m => m.Name == "ToString");
        Assert.Equal("ToString", page.ViewModel.OpenedGraph?.Name);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task DeleteUndoRedoKeys()
    {
        using var session = await EditorSession.OpenSampleMainAsync();
        var page = session.ClassEditor;
        var method = (MethodGraph)session.Graph.ViewModel.Graph;

        page.ViewModel.CreateVariableCommand.Execute(null); // PAR-37
        page.Window.Focus();
        page.PressUndo();
        Assert.Empty(page.ViewModel.Variables);
        page.PressRedo();
        Assert.Single(page.ViewModel.Variables);

        session.Graph.BoxSelectAll();
        page.PressDelete();
        await session.Graph.WaitForRenderedAsync();

        Assert.DoesNotContain(method.Nodes, n => n is CallMethodNode);
        Assert.Equal(2, method.Nodes.Count); // entry and main return are kept
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task RunCompilesAndStarts()
    {
        using var session = await EditorSession.OpenSampleMainAsync();

        await session.ClassEditor.ViewModel.RunCommand.ExecuteAsync(null);
        await HeadlessInput.WaitUntilAsync(() => session.Main.Processes.Started.Count == 1, "program started", 60_000);

        Assert.Equal("Build succeeded", session.ClassEditor.StatusText); // PAR-10, PAR-32
    }
}
