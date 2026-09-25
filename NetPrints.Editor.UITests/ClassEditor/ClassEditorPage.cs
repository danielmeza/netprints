using Avalonia.Controls;
using Avalonia.Input;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.Graph;
using NetPrints.Editor.UITests.Graph;

namespace NetPrints.Editor.UITests.ClassEditor;

/// <summary>Page object of a class editor window.</summary>
public sealed class ClassEditorPage(ClassEditorWindow window)
{
    public ClassEditorWindow Window { get; } = window;
    public ClassEditorVM ViewModel => (ClassEditorVM)Window.DataContext!;

    public Button CompileButton => Window.ById<Button>(AutomationIds.ClassEditorCompileButton);
    public ComboBox OverrideChooser => Window.ById<ComboBox>(AutomationIds.ClassEditorOverrideChooser);
    public string? StatusText => Window.ById<TextBlock>(AutomationIds.ClassEditorStatusText).Text;
    public bool IsClassInspectorVisible => Window.ById<Control>(AutomationIds.ClassInspector).IsVisible;
    public bool IsMethodInspectorVisible => Window.ById<Control>(AutomationIds.MethodInspector).IsVisible;
    public bool IsVariableInspectorVisible => Window.ById<Control>(AutomationIds.VariableInspector).IsVisible;
    public TextBox ClassNameBox => Window.ById<TextBox>(AutomationIds.ClassInspectorName);
    public TextBox GeneratedCodeBox => Window.ById<TextBox>(AutomationIds.ClassInspectorGeneratedCode);
    public int SplitterCount => Window.Descendants<GridSplitter>().Count();

    /// <summary>The graph canvas.</summary>
    public GraphCanvasPage Graph => new(Window, Window.ById<GraphEditorView>(AutomationIds.ClassEditorGraph));

    /// <summary>Uses a fixed window size so pointer coordinates are predictable.</summary>
    public void UseFixedSize()
    {
        Window.WindowState = WindowState.Normal;
        Window.Width = 1600;
        Window.Height = 1000;
        HeadlessInput.Pump();
    }

    private TextBlock MethodName(string name) =>
        Window.AllById<TextBlock>(AutomationIds.ClassEditorMethodName).Single(t => t.Text == name);

    public void ClickMethod(string name) => Window.Click(MethodName(name).CenterIn(Window));

    /// <summary>Double-clicks a method and waits for its graph to be rendered.</summary>
    public async Task<GraphCanvasPage> OpenMethodAsync(string name)
    {
        Window.DoubleClick(MethodName(name).CenterIn(Window));
        await HeadlessInput.WaitUntilAsync(() => ViewModel.OpenedGraph?.Name == name, $"graph {name} opened");
        var graph = Graph;
        await graph.WaitForRenderedAsync();
        return graph;
    }

    public void ClickVariable(string name) =>
        Window.Click(Window.AllById<TextBlock>(AutomationIds.VariableName).Single(t => t.Text == name).CenterIn(Window));

    public void ClickClassButton() => Window.Click(Window.ById<Button>(AutomationIds.ClassEditorClassButton).CenterIn(Window));

    public void PressUndo() => Window.Press(Key.Z, RawInputModifiers.Control);

    public void PressRedo() => Window.Press(Key.Y, RawInputModifiers.Control);

    public void PressDelete() => Window.Press(Key.Delete);
}
