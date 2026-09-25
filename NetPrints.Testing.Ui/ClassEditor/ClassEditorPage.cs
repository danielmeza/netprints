using NetPrints.Editor;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Testing.Ui.Driving;
using NetPrints.Testing.Ui.Graph;

namespace NetPrints.Testing.Ui.ClassEditor;

/// <summary>Screen object of the class editor window of one class.</summary>
public sealed class ClassEditorPage(IUiDriver driver, string classFullName)
    : UiElement(driver, new AutomationQuery(AutomationIds.ClassEditorWindow) { Name = classFullName })
{
    public string ClassFullName { get; } = classFullName;

    public UiElement CompileButton => Find(AutomationIds.ClassEditorCompileButton);
    public UiElement RunButton => Find(AutomationIds.ClassEditorRunButton);
    public UiElement ClassButton => Find(AutomationIds.ClassEditorClassButton);
    public UiElement SaveButton => Find(AutomationIds.ClassEditorSaveButton);
    public UiElement MethodList => Find(AutomationIds.ClassEditorMethodList);
    public UiElement ConstructorList => Find(AutomationIds.ClassEditorConstructorList);
    public UiElement VariableList => Find(AutomationIds.ClassEditorVariableList);
    public UiElement OverrideChooser => Find(AutomationIds.ClassEditorOverrideChooser);
    public UiElement CreateMethodButton => Find(AutomationIds.ClassEditorCreateMethodButton);
    public UiElement CreateConstructorButton => Find(AutomationIds.ClassEditorCreateConstructorButton);
    public UiElement CreateVariableButton => Find(AutomationIds.ClassEditorCreateVariableButton);
    public UiElement ErrorList => Find(AutomationIds.ClassEditorErrorList);
    public UiElement OutputTab => Find(AutomationIds.ClassEditorOutputTab);
    public UiElement OutputText => Find(AutomationIds.ClassEditorOutputText);
    public UiElement ClearOutputButton => Find(AutomationIds.ClassEditorClearOutputButton);
    public UiElement StatusText => Find(AutomationIds.ClassEditorStatusText);
    public UiElement LeftColumn => Find(AutomationIds.ClassEditorLeftColumn);
    public UiElement InspectorColumn => Find(AutomationIds.ClassEditorInspectorColumn);

    public UiElement MethodsSplitter => Find(AutomationIds.ClassEditorMethodsSplitter);
    public UiElement ConstructorsSplitter => Find(AutomationIds.ClassEditorConstructorsSplitter);
    public UiElement LeftSplitter => Find(AutomationIds.ClassEditorLeftSplitter);
    public UiElement ErrorsSplitter => Find(AutomationIds.ClassEditorErrorsSplitter);
    public UiElement InspectorSplitter => Find(AutomationIds.ClassEditorInspectorSplitter);

    public IReadOnlyList<UiElement> Splitters => [MethodsSplitter, ConstructorsSplitter, LeftSplitter, ErrorsSplitter, InspectorSplitter];

    public ClassInspectorPanel ClassInspector => new(Driver, Query);
    public InspectorPanel MethodInspector => new(Driver, Query, AutomationIds.MethodInspector);
    public InspectorPanel VariableInspector => new(Driver, Query, AutomationIds.VariableInspector);

    /// <summary>The graph canvas.</summary>
    public GraphCanvas Graph => new(Driver, Query);

    /// <summary>A method row (name text) in the method list.</summary>
    public UiElement Method(string name) => MethodList.Find(AutomationIds.ClassEditorMethodName, text: name);

    /// <summary>A constructor row in the constructor list (constructors are listed by index).</summary>
    public UiElement Constructor(int index) => ConstructorList.Find(AutomationIds.ClassEditorMethodName, index: index);

    /// <summary>A variable row in the variable list.</summary>
    public UiElement Variable(string name) => VariableList.Find(AutomationIds.VariableRow, name: name);

    public UiElement VariableNameText(string name) => Variable(name).Find(AutomationIds.VariableName);

    public async Task<IReadOnlyList<string>> MethodNamesAsync(CancellationToken cancellationToken) =>
        (await Driver.FindAllAsync(new AutomationQuery(AutomationIds.ClassEditorMethodName) { Within = MethodList.Query }, cancellationToken))
            .Select(e => e.Text ?? "").ToList();

    public async Task<IReadOnlyList<string>> VariableNamesAsync(CancellationToken cancellationToken) =>
        (await Driver.FindAllAsync(new AutomationQuery(AutomationIds.VariableRow) { Within = VariableList.Query }, cancellationToken))
            .Select(e => e.Name ?? "").ToList();

    /// <summary>Double-clicks a method and waits for its graph.</summary>
    public async Task<GraphCanvas> OpenMethodAsync(string name, CancellationToken cancellationToken)
    {
        await Method(name).DoubleClickAsync(cancellationToken);
        return await Graph.WaitForGraphAsync(name, cancellationToken);
    }

    /// <summary>Clicks Compile and waits for the build result ("Build succeeded" or "Build failed …").</summary>
    public async Task<string> CompileAsync(CancellationToken cancellationToken)
    {
        await CompileButton.ClickAsync(cancellationToken);
        return await WaitForBuildResultAsync(cancellationToken);
    }

    public async Task<string> WaitForBuildResultAsync(CancellationToken cancellationToken)
    {
        await StatusText.WaitUntilAsync(e => e.Text?.StartsWith("Build ", StringComparison.Ordinal) == true, "a build result",
            cancellationToken, TimeSpan.FromSeconds(120));
        return (await StatusText.TextAsync(cancellationToken))!;
    }

    /// <summary>Waits until Run's Output tab shows <paramref name="expected"/> (the program's console output).</summary>
    public async Task<string> WaitForOutputContainingAsync(string expected, CancellationToken cancellationToken)
    {
        await OutputText.WaitUntilAsync(e => (e.Text ?? "").Contains(expected, StringComparison.Ordinal),
            $"output containing '{expected}'", cancellationToken, TimeSpan.FromSeconds(60));
        return (await OutputText.TextAsync(cancellationToken))!;
    }

    public Task PressUndoAsync(CancellationToken cancellationToken) => Driver.PressAsync("Ctrl+Z", cancellationToken);

    public Task PressRedoAsync(CancellationToken cancellationToken) => Driver.PressAsync("Ctrl+Y", cancellationToken);

    public Task PressDeleteAsync(CancellationToken cancellationToken) => Driver.PressAsync("Delete", cancellationToken);

    public async Task<string?> WindowStateAsync(CancellationToken cancellationToken) => await PropertyAsync("WindowState", cancellationToken);
}

/// <summary>Component object of an inspector in the right column.</summary>
public class InspectorPanel(IUiDriver driver, AutomationQuery window, string automationId)
    : UiElement(driver, new AutomationQuery(automationId) { Within = window, IncludeHidden = true });

/// <summary>The class inspector: name box and generated code preview.</summary>
public sealed class ClassInspectorPanel(IUiDriver driver, AutomationQuery window)
    : InspectorPanel(driver, window, AutomationIds.ClassInspector)
{
    public UiElement NameBox => Find(AutomationIds.ClassInspectorName);

    public UiElement GeneratedCode => Find(AutomationIds.ClassInspectorGeneratedCode);
}
