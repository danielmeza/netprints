namespace NetPrints.Editor;

/// <summary>
/// Automation ids of the editor's UI: the contract between the XAML (<c>x:Static</c>) and the UI
/// test page objects, so a rename breaks the build instead of a test run.
/// </summary>
public static class AutomationIds
{
    // Main window
    public const string MainProjectButton = "Main.ProjectButton";
    public const string MainReferencesButton = "Main.ReferencesButton";
    public const string MainSettingsButton = "Main.SettingsButton";
    public const string MainCompileButton = "Main.CompileButton";
    public const string MainRunButton = "Main.RunButton";
    public const string MainProjectPane = "Main.ProjectPane";
    public const string MainSettingsPane = "Main.SettingsPane";
    public const string MainSaveProjectButton = "Main.SaveProjectButton";
    public const string MainNewClassButton = "Main.NewClassButton";
    public const string MainClassList = "Main.ClassList";
    public const string MainOpenClassButton = "Main.OpenClassButton";
    public const string MainBusyOverlay = "Main.BusyOverlay";

    // Class editor window
    public const string ClassEditorCompileButton = "ClassEditor.CompileButton";
    public const string ClassEditorRunButton = "ClassEditor.RunButton";
    public const string ClassEditorClassButton = "ClassEditor.ClassButton";
    public const string ClassEditorSaveButton = "ClassEditor.SaveButton";
    public const string ClassEditorMethodList = "ClassEditor.MethodList";
    public const string ClassEditorMethodName = "ClassEditor.MethodName";
    public const string ClassEditorVariableList = "ClassEditor.VariableList";
    public const string ClassEditorOverrideChooser = "ClassEditor.OverrideChooser";
    public const string ClassEditorErrorList = "ClassEditor.ErrorList";
    public const string ClassEditorStatusText = "ClassEditor.StatusText";
    public const string ClassEditorGraph = "ClassEditor.Graph";

    // Inspectors
    public const string ClassInspector = "Inspectors.Class";
    public const string ClassInspectorName = "Inspectors.Class.Name";
    public const string ClassInspectorGeneratedCode = "Inspectors.Class.GeneratedCode";
    public const string MethodInspector = "Inspectors.Method";
    public const string VariableInspector = "Inspectors.Variable";

    // Variables list
    public const string VariableName = "Variables.Name";

    // Graph canvas
    public const string GraphEditor = "Graph.Editor";
    public const string GraphWatermark = "Graph.Watermark";
    public const string GraphSearchPopup = "Graph.SearchPopup";
    public const string GraphGetSetPopup = "Graph.GetSetPopup";
    public const string NodeLabel = "Graph.Node.Label";
    public const string NodeOverloads = "Graph.Node.Overloads";
    public const string NodePure = "Graph.Node.Pure";
    public const string NodeLeftPlus = "Graph.Node.LeftPlus";
    public const string NodeLeftMinus = "Graph.Node.LeftMinus";
    public const string PinValueText = "Graph.Pin.ValueText";
    public const string GetButton = "Graph.GetSet.Get";
    public const string SetButton = "Graph.GetSet.Set";

    // Node search
    public const string SearchBox = "Search.Box";
    public const string SearchResults = "Search.Results";
    public const string SearchRowText = "Search.RowText";

    // Dialogs
    public const string ErrorMessage = "Dialogs.Error.Message";
    public const string ErrorOkButton = "Dialogs.Error.Ok";
    public const string SelectTypeBox = "Dialogs.SelectType.Box";
    public const string SelectMethodBox = "Dialogs.SelectMethod.Box";
    public const string ReferencesCloseButton = "References.Close";
}
