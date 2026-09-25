namespace NetPrints.Editor;

/// <summary>
/// Automation ids of the editor's UI: the contract between the XAML (<c>x:Static</c>) and the UI
/// test page objects, so a rename breaks the build instead of a test run.
/// </summary>
public static class AutomationIds
{
    // Main window
    /// <summary>
    /// Automation id for the main window's button that opens the project pane.
    /// </summary>
    public const string MainProjectButton = "Main.ProjectButton";
    /// <summary>
    /// Automation id for the main window's button that opens the references dialog.
    /// </summary>
    public const string MainReferencesButton = "Main.ReferencesButton";
    /// <summary>
    /// Automation id for the main window's button that opens the settings pane.
    /// </summary>
    public const string MainSettingsButton = "Main.SettingsButton";
    /// <summary>
    /// Automation id for the main window's compile button.
    /// </summary>
    public const string MainCompileButton = "Main.CompileButton";
    /// <summary>
    /// Automation id for the main window's run button.
    /// </summary>
    public const string MainRunButton = "Main.RunButton";
    /// <summary>
    /// Automation id for the main window's project pane.
    /// </summary>
    public const string MainProjectPane = "Main.ProjectPane";
    /// <summary>
    /// Automation id for the main window's settings pane.
    /// </summary>
    public const string MainSettingsPane = "Main.SettingsPane";
    /// <summary>
    /// Automation id for the main window's save-project button.
    /// </summary>
    public const string MainSaveProjectButton = "Main.SaveProjectButton";
    /// <summary>
    /// Automation id for the main window's button that creates a new class.
    /// </summary>
    public const string MainNewClassButton = "Main.NewClassButton";
    /// <summary>
    /// Automation id for the main window's list of the project's classes.
    /// </summary>
    public const string MainClassList = "Main.ClassList";
    /// <summary>
    /// Automation id for the main window's button that opens the selected class.
    /// </summary>
    public const string MainOpenClassButton = "Main.OpenClassButton";
    /// <summary>
    /// Automation id for the main window's busy overlay, shown while an operation is running.
    /// </summary>
    public const string MainBusyOverlay = "Main.BusyOverlay";

    // Class editor window
    /// <summary>
    /// Automation id for the class editor window's compile button.
    /// </summary>
    public const string ClassEditorCompileButton = "ClassEditor.CompileButton";
    /// <summary>
    /// Automation id for the class editor window's run button.
    /// </summary>
    public const string ClassEditorRunButton = "ClassEditor.RunButton";
    /// <summary>
    /// Automation id for the class editor window's button that opens the class inspector.
    /// </summary>
    public const string ClassEditorClassButton = "ClassEditor.ClassButton";
    /// <summary>
    /// Automation id for the class editor window's save button.
    /// </summary>
    public const string ClassEditorSaveButton = "ClassEditor.SaveButton";
    /// <summary>
    /// Automation id for the class editor window's list of the class's methods.
    /// </summary>
    public const string ClassEditorMethodList = "ClassEditor.MethodList";
    /// <summary>
    /// Automation id for a method list row's name text.
    /// </summary>
    public const string ClassEditorMethodName = "ClassEditor.MethodName";
    /// <summary>
    /// Automation id for the class editor window's list of the class's variables.
    /// </summary>
    public const string ClassEditorVariableList = "ClassEditor.VariableList";
    /// <summary>
    /// Automation id for the class editor window's chooser for adding an override method.
    /// </summary>
    public const string ClassEditorOverrideChooser = "ClassEditor.OverrideChooser";
    /// <summary>
    /// Automation id for the class editor window's list of compile errors.
    /// </summary>
    public const string ClassEditorErrorList = "ClassEditor.ErrorList";
    /// <summary>
    /// Automation id for the class editor window's output tab.
    /// </summary>
    public const string ClassEditorOutputTab = "ClassEditor.OutputTab";
    /// <summary>
    /// Automation id for the class editor window's output text pane.
    /// </summary>
    public const string ClassEditorOutputText = "ClassEditor.OutputText";
    /// <summary>
    /// Automation id for the class editor window's button that clears the output pane.
    /// </summary>
    public const string ClassEditorClearOutputButton = "ClassEditor.ClearOutputButton";
    /// <summary>
    /// Automation id for the class editor window's status text.
    /// </summary>
    public const string ClassEditorStatusText = "ClassEditor.StatusText";
    /// <summary>
    /// Automation id for the class editor window's graph canvas host.
    /// </summary>
    public const string ClassEditorGraph = "ClassEditor.Graph";

    // Inspectors
    /// <summary>
    /// Automation id for the class inspector pane.
    /// </summary>
    public const string ClassInspector = "Inspectors.Class";
    /// <summary>
    /// Automation id for the class inspector's name field.
    /// </summary>
    public const string ClassInspectorName = "Inspectors.Class.Name";
    /// <summary>
    /// Automation id for the class inspector's generated-code preview.
    /// </summary>
    public const string ClassInspectorGeneratedCode = "Inspectors.Class.GeneratedCode";
    /// <summary>
    /// Automation id for the method inspector pane.
    /// </summary>
    public const string MethodInspector = "Inspectors.Method";
    /// <summary>
    /// Automation id for the variable inspector pane.
    /// </summary>
    public const string VariableInspector = "Inspectors.Variable";

    // Variables list
    /// <summary>
    /// Automation id for a variable list row's name field.
    /// </summary>
    public const string VariableName = "Variables.Name";

    // Graph canvas
    /// <summary>
    /// Automation id for the graph canvas editor.
    /// </summary>
    public const string GraphEditor = "Graph.Editor";
    /// <summary>
    /// Automation id for the graph canvas's background grid.
    /// </summary>
    public const string GraphGrid = "Graph.Grid";
    /// <summary>
    /// Automation id for the graph canvas's empty-graph watermark.
    /// </summary>
    public const string GraphWatermark = "Graph.Watermark";
    /// <summary>
    /// Automation id for the graph canvas's node search popup.
    /// </summary>
    public const string GraphSearchPopup = "Graph.SearchPopup";
    /// <summary>
    /// Automation id for the graph canvas's get/set chooser popup.
    /// </summary>
    public const string GraphGetSetPopup = "Graph.GetSetPopup";
    /// <summary>
    /// Automation id for a node's title label.
    /// </summary>
    public const string NodeLabel = "Graph.Node.Label";
    /// <summary>
    /// Automation id for a node's overload chooser.
    /// </summary>
    public const string NodeOverloads = "Graph.Node.Overloads";
    /// <summary>
    /// Automation id for a node's purity toggle.
    /// </summary>
    public const string NodePure = "Graph.Node.Pure";
    /// <summary>
    /// Automation id for a node's add-pin button on its left side.
    /// </summary>
    public const string NodeLeftPlus = "Graph.Node.LeftPlus";
    /// <summary>
    /// Automation id for a node's remove-pin button on its left side.
    /// </summary>
    public const string NodeLeftMinus = "Graph.Node.LeftMinus";
    /// <summary>
    /// Automation id for a pin's inline text value editor.
    /// </summary>
    public const string PinValueText = "Graph.Pin.ValueText";
    /// <summary>
    /// Automation id for a pin's inline boolean value checkbox.
    /// </summary>
    public const string PinValueCheck = "Graph.Pin.ValueCheck";
    /// <summary>
    /// Automation id for a pin's inline enum value chooser.
    /// </summary>
    public const string PinValueEnum = "Graph.Pin.ValueEnum";
    /// <summary>
    /// Automation id for the get/set chooser's get button.
    /// </summary>
    public const string GetButton = "Graph.GetSet.Get";
    /// <summary>
    /// Automation id for the get/set chooser's set button.
    /// </summary>
    public const string SetButton = "Graph.GetSet.Set";

    // Node search
    /// <summary>
    /// Automation id for the node search popup's search text box.
    /// </summary>
    public const string SearchBox = "Search.Box";
    /// <summary>
    /// Automation id for the node search popup's results list.
    /// </summary>
    public const string SearchResults = "Search.Results";
    /// <summary>
    /// Automation id for a search result row's text.
    /// </summary>
    public const string SearchRowText = "Search.RowText";
    /// <summary>
    /// Automation id for a search result row's icon. The row's AutomationProperties.Name carries the row text.
    /// </summary>
    public const string SearchRowIcon = "Search.RowIcon";

    // Dialogs
    /// <summary>
    /// Automation id for the error dialog's message text.
    /// </summary>
    public const string ErrorMessage = "Dialogs.Error.Message";
    /// <summary>
    /// Automation id for the error dialog's OK button.
    /// </summary>
    public const string ErrorOkButton = "Dialogs.Error.Ok";
    /// <summary>
    /// Automation id for the select-type dialog's search box.
    /// </summary>
    public const string SelectTypeBox = "Dialogs.SelectType.Box";
    /// <summary>
    /// Automation id for the select-method dialog's search box.
    /// </summary>
    public const string SelectMethodBox = "Dialogs.SelectMethod.Box";
    /// <summary>
    /// Automation id for the references dialog's close button.
    /// </summary>
    public const string ReferencesCloseButton = "References.Close";

    // Windows (AutomationProperties.Name carries the class full name for class windows)
    /// <summary>
    /// Automation id for the main window itself.
    /// </summary>
    public const string MainWindow = "Main.Window";
    /// <summary>
    /// Automation id for the class editor window itself. Its AutomationProperties.Name carries the class's full name.
    /// </summary>
    public const string ClassEditorWindow = "ClassEditor.Window";
    /// <summary>
    /// Automation id for the references dialog window itself.
    /// </summary>
    public const string ReferencesDialog = "References.Dialog";
    /// <summary>
    /// Automation id for the error dialog window itself.
    /// </summary>
    public const string ErrorDialog = "Dialogs.Error";
    /// <summary>
    /// Automation id for the select-type dialog window itself.
    /// </summary>
    public const string SelectTypeDialog = "Dialogs.SelectType";
    /// <summary>
    /// Automation id for the select-method dialog window itself.
    /// </summary>
    public const string SelectMethodDialog = "Dialogs.SelectMethod";
    /// <summary>
    /// Automation id for the select-type dialog's select button.
    /// </summary>
    public const string SelectTypeButton = "Dialogs.SelectType.Select";
    /// <summary>
    /// Automation id for the select-method dialog's select button.
    /// </summary>
    public const string SelectMethodButton = "Dialogs.SelectMethod.Select";

    // Main window panes
    /// <summary>
    /// Automation id for the main window's button that creates a new project.
    /// </summary>
    public const string MainCreateProjectButton = "Main.CreateProjectButton";
    /// <summary>
    /// Automation id for the main window's button that opens an existing project.
    /// </summary>
    public const string MainOpenProjectButton = "Main.OpenProjectButton";
    /// <summary>
    /// Automation id for the main window's button that adds an existing class to the project.
    /// </summary>
    public const string MainExistingClassButton = "Main.ExistingClassButton";
    /// <summary>
    /// Automation id for the main window's button that removes the selected class from the project.
    /// </summary>
    public const string MainRemoveClassButton = "Main.RemoveClassButton";
    /// <summary>
    /// Automation id for the main window's compilation-output chooser.
    /// </summary>
    public const string MainOutputChooser = "Main.OutputChooser";
    /// <summary>
    /// Automation id for the main window's binary-type chooser.
    /// </summary>
    public const string MainBinaryTypeChooser = "Main.BinaryTypeChooser";

    // Class editor lists and splitters
    /// <summary>
    /// Automation id for the class editor window's list of the class's constructors.
    /// </summary>
    public const string ClassEditorConstructorList = "ClassEditor.ConstructorList";
    /// <summary>
    /// Automation id for the class editor window's button that creates a new method.
    /// </summary>
    public const string ClassEditorCreateMethodButton = "ClassEditor.CreateMethodButton";
    /// <summary>
    /// Automation id for the class editor window's button that creates a new constructor.
    /// </summary>
    public const string ClassEditorCreateConstructorButton = "ClassEditor.CreateConstructorButton";
    /// <summary>
    /// Automation id for the class editor window's button that creates a new variable.
    /// </summary>
    public const string ClassEditorCreateVariableButton = "ClassEditor.CreateVariableButton";
    /// <summary>
    /// Automation id for the class editor window's left column (methods/constructors/variables lists).
    /// </summary>
    public const string ClassEditorLeftColumn = "ClassEditor.LeftColumn";
    /// <summary>
    /// Automation id for the class editor window's inspector column.
    /// </summary>
    public const string ClassEditorInspectorColumn = "ClassEditor.InspectorColumn";
    /// <summary>
    /// Automation id for the splitter above the class editor window's method list.
    /// </summary>
    public const string ClassEditorMethodsSplitter = "ClassEditor.Splitter.Methods";
    /// <summary>
    /// Automation id for the splitter above the class editor window's constructor list.
    /// </summary>
    public const string ClassEditorConstructorsSplitter = "ClassEditor.Splitter.Constructors";
    /// <summary>
    /// Automation id for the splitter between the class editor window's left column and graph.
    /// </summary>
    public const string ClassEditorLeftSplitter = "ClassEditor.Splitter.Left";
    /// <summary>
    /// Automation id for the splitter above the class editor window's error list.
    /// </summary>
    public const string ClassEditorErrorsSplitter = "ClassEditor.Splitter.Errors";
    /// <summary>
    /// Automation id for the splitter between the class editor window's graph and inspector column.
    /// </summary>
    public const string ClassEditorInspectorSplitter = "ClassEditor.Splitter.Inspector";

    // Variables list rows (AutomationProperties.Name carries the variable name)
    /// <summary>
    /// Automation id for a variable list row. Its AutomationProperties.Name carries the variable's name.
    /// </summary>
    public const string VariableRow = "Variables.Row";
    /// <summary>
    /// Automation id for a variable list row's getter toggle.
    /// </summary>
    public const string VariableGetter = "Variables.Getter";
    /// <summary>
    /// Automation id for a variable list row's setter toggle.
    /// </summary>
    public const string VariableSetter = "Variables.Setter";

    // Graph items (AutomationProperties.Name carries a stable identity)
    /// <summary>
    /// Automation id for a graph node. Its AutomationProperties.Name carries the node's own Name (eg. "CallMethodNode").
    /// </summary>
    public const string Node = "Graph.Node";
    /// <summary>
    /// Automation id for a node pin. Its AutomationProperties.Name is "in:&lt;pin&gt;" or "out:&lt;pin&gt;".
    /// </summary>
    public const string Pin = "Graph.Pin";
    /// <summary>
    /// Automation id for a pin's connector handle.
    /// </summary>
    public const string PinConnector = "Graph.PinConnector";
    /// <summary>
    /// Automation id for a connection between two pins. Its AutomationProperties.Name is "&lt;node&gt;.&lt;pin&gt;-&gt;&lt;node&gt;.&lt;pin&gt;".
    /// </summary>
    public const string Connection = "Graph.Connection";
    /// <summary>
    /// Automation id for the get/set chooser popup.
    /// </summary>
    public const string GetSetChooser = "Graph.GetSetChooser";
    /// <summary>
    /// Automation id for the node search popup view.
    /// </summary>
    public const string NodeSearch = "Search.View";

    // References dialog rows (AutomationProperties.Name carries the reference's display text)
    /// <summary>
    /// Automation id for the references dialog's button that adds an assembly reference.
    /// </summary>
    public const string ReferencesAddAssemblyButton = "References.AddAssembly";
    /// <summary>
    /// Automation id for the references dialog's button that adds a source directory reference.
    /// </summary>
    public const string ReferencesAddSourceButton = "References.AddSource";
    /// <summary>
    /// Automation id for a references dialog row. Its AutomationProperties.Name carries the reference's display text.
    /// </summary>
    public const string ReferenceRow = "References.Row";
    /// <summary>
    /// Automation id for a references dialog row's include-in-compilation toggle.
    /// </summary>
    public const string ReferenceInclude = "References.Include";
    /// <summary>
    /// Automation id for a references dialog row's remove button.
    /// </summary>
    public const string ReferenceRemove = "References.Remove";
}
