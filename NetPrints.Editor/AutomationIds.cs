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
    public const string ClassEditorOutputTab = "ClassEditor.OutputTab";
    public const string ClassEditorOutputText = "ClassEditor.OutputText";
    public const string ClassEditorClearOutputButton = "ClassEditor.ClearOutputButton";
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
    public const string PinValueCheck = "Graph.Pin.ValueCheck";
    public const string PinValueEnum = "Graph.Pin.ValueEnum";
    public const string GetButton = "Graph.GetSet.Get";
    public const string SetButton = "Graph.GetSet.Set";

    // Node search
    public const string SearchBox = "Search.Box";
    public const string SearchResults = "Search.Results";
    public const string SearchRowText = "Search.RowText";
    public const string SearchRowIcon = "Search.RowIcon";            // Name = the row text

    // Dialogs
    public const string ErrorMessage = "Dialogs.Error.Message";
    public const string ErrorOkButton = "Dialogs.Error.Ok";
    public const string SelectTypeBox = "Dialogs.SelectType.Box";
    public const string SelectMethodBox = "Dialogs.SelectMethod.Box";
    public const string ReferencesCloseButton = "References.Close";

    // Windows (AutomationProperties.Name carries the class full name for class windows)
    public const string MainWindow = "Main.Window";
    public const string ClassEditorWindow = "ClassEditor.Window";
    public const string ReferencesDialog = "References.Dialog";
    public const string ErrorDialog = "Dialogs.Error";
    public const string SelectTypeDialog = "Dialogs.SelectType";
    public const string SelectMethodDialog = "Dialogs.SelectMethod";
    public const string SelectTypeButton = "Dialogs.SelectType.Select";
    public const string SelectMethodButton = "Dialogs.SelectMethod.Select";

    // Main window panes
    public const string MainCreateProjectButton = "Main.CreateProjectButton";
    public const string MainOpenProjectButton = "Main.OpenProjectButton";
    public const string MainExistingClassButton = "Main.ExistingClassButton";
    public const string MainRemoveClassButton = "Main.RemoveClassButton";
    public const string MainOutputChooser = "Main.OutputChooser";
    public const string MainBinaryTypeChooser = "Main.BinaryTypeChooser";

    // Class editor lists and splitters
    public const string ClassEditorConstructorList = "ClassEditor.ConstructorList";
    public const string ClassEditorCreateMethodButton = "ClassEditor.CreateMethodButton";
    public const string ClassEditorCreateConstructorButton = "ClassEditor.CreateConstructorButton";
    public const string ClassEditorCreateVariableButton = "ClassEditor.CreateVariableButton";
    public const string ClassEditorLeftColumn = "ClassEditor.LeftColumn";
    public const string ClassEditorInspectorColumn = "ClassEditor.InspectorColumn";
    public const string ClassEditorMethodsSplitter = "ClassEditor.Splitter.Methods";
    public const string ClassEditorConstructorsSplitter = "ClassEditor.Splitter.Constructors";
    public const string ClassEditorLeftSplitter = "ClassEditor.Splitter.Left";
    public const string ClassEditorErrorsSplitter = "ClassEditor.Splitter.Errors";
    public const string ClassEditorInspectorSplitter = "ClassEditor.Splitter.Inspector";

    // Variables list rows (AutomationProperties.Name carries the variable name)
    public const string VariableRow = "Variables.Row";
    public const string VariableGetter = "Variables.Getter";
    public const string VariableSetter = "Variables.Setter";

    // Graph items (AutomationProperties.Name carries a stable identity)
    public const string Node = "Graph.Node";                       // Name = Node.Name, e.g. "CallMethodNode"
    public const string Pin = "Graph.Pin";                         // Name = "in:<pin>" or "out:<pin>"
    public const string PinConnector = "Graph.PinConnector";
    public const string Connection = "Graph.Connection";           // Name = "<node>.<pin>-><node>.<pin>"
    public const string GetSetChooser = "Graph.GetSetChooser";
    public const string NodeSearch = "Search.View";

    // References dialog rows (AutomationProperties.Name carries the reference's display text)
    public const string ReferencesAddAssemblyButton = "References.AddAssembly";
    public const string ReferencesAddSourceButton = "References.AddSource";
    public const string ReferenceRow = "References.Row";
    public const string ReferenceInclude = "References.Include";
    public const string ReferenceRemove = "References.Remove";
}
