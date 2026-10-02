namespace NetPrints.Editor;

/// <summary>
/// Automation ids of the editor's UI: the contract between the XAML (<c>x:Static</c>) and the UI
/// test page objects, so a rename breaks the build instead of a test run.
/// </summary>
public static class AutomationIds
{
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
    /// Automation id for the class inspector's read-only C# code view (US6).
    /// </summary>
    public const string ClassInspectorCodeView = "Inspectors.Class.CodeView";
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
    /// Automation id for a pin's name/type label (OWN-05: the plain <c>TextBlock</c> or, when the name
    /// is editable, the <c>TextBox</c>), so a test can compare its bounds against
    /// <see cref="PinConnector"/>'s.
    /// </summary>
    public const string PinLabel = "Graph.Pin.Label";
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
    /// Automation id for the issues dialog window itself.
    /// </summary>
    public const string IssuesDialog = "Dialogs.Issues";
    /// <summary>
    /// Automation id for the issues dialog's list; at startup it lists the extensions that failed to load.
    /// </summary>
    public const string ExtensionLoadErrors = "Dialogs.Issues.List";
    /// <summary>
    /// Automation id for a row of the issues dialog's list. The row's AutomationProperties.Name carries its text.
    /// </summary>
    public const string IssueRow = "Dialogs.Issues.Row";
    /// <summary>
    /// Automation id for the issues dialog's OK button.
    /// </summary>
    public const string IssuesOkButton = "Dialogs.Issues.Ok";
    /// <summary>
    /// Automation id for the trust dialog window itself.
    /// </summary>
    public const string TrustDialog = "Dialogs.Trust";
    /// <summary>
    /// Automation id for the trust dialog's explanation text.
    /// </summary>
    public const string TrustPrompt = "Dialogs.Trust.Prompt";
    /// <summary>
    /// Automation id for the trust dialog's list of extension folders.
    /// </summary>
    public const string TrustFolders = "Dialogs.Trust.Folders";
    /// <summary>
    /// Automation id for the trust dialog's Trust button.
    /// </summary>
    public const string TrustButton = "Dialogs.Trust.Trust";
    /// <summary>
    /// Automation id for the trust dialog's Don't load button.
    /// </summary>
    public const string TrustDontLoadButton = "Dialogs.Trust.DontLoad";
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

    // Dialog windows
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

    /// <summary>
    /// Automation id for the binary type chooser of the Project settings document.
    /// </summary>
    public const string ProjectSettingsBinaryTypeChooser = "ProjectSettings.BinaryTypeChooser";

    // Shell (contracts/shell.md section 6)
    /// <summary>Automation id of the shell window.</summary>
    public const string ShellWindow = "Shell.Window";

    /// <summary>
    /// Prefix of the automation id of a tool pane's content: the panel id follows, such as <c>Shell.Panel.netprints.panel.errors</c>.
    /// </summary>
    public const string ShellPanelPrefix = "Shell.Panel.";
    /// <summary>
    /// Prefix of the automation id of a document's content: the document id follows, such as <c>Shell.Document.graph:A.cs#method:1</c>.
    /// </summary>
    public const string ShellDocumentPrefix = "Shell.Document.";
    /// <summary>
    /// Prefix of a menu's automation id: the menu name, or for an item the command id, follows, such as <c>Menu.File</c> and <c>Menu.netprints.command.save</c>.
    /// </summary>
    public const string MenuPrefix = "Menu.";
    /// <summary>
    /// Prefix of a command bar button's automation id: the command id follows, such as <c>CommandBar.netprints.command.save</c>.
    /// </summary>
    public const string CommandBarPrefix = "CommandBar.";
    /// <summary>
    /// Automation id for the shell window's menu bar.
    /// </summary>
    public const string ShellMenuBar = "Shell.MenuBar";
    /// <summary>
    /// Automation id for the shell window's command bar.
    /// </summary>
    public const string ShellCommandBar = "Shell.CommandBar";
    /// <summary>
    /// Automation id for the shell window's status bar.
    /// </summary>
    public const string ShellStatusBar = "Shell.StatusBar";
    /// <summary>
    /// Automation id for the status bar's message.
    /// </summary>
    public const string ShellStatusMessage = "Shell.StatusMessage";
    /// <summary>
    /// Automation id for the status bar's build state.
    /// </summary>
    public const string ShellBuildState = "Shell.BuildState";
    /// <summary>
    /// Automation id for the error badge of the command bar's compile button.
    /// </summary>
    public const string ShellCompileBadge = "Shell.CompileBadge";
    /// <summary>
    /// Automation id for the shortcut text of a menu item.
    /// </summary>
    public const string ShellMenuShortcut = "Shell.MenuShortcut";

    // Project tree and inspector panels (contracts/shell.md section 6)
    /// <summary>
    /// Automation id for the project tree control.
    /// </summary>
    public const string TreeView = "Tree.View";
    /// <summary>
    /// Prefix of a tree item's automation id: the kind and the name follow, such as <c>Tree.method.Main</c>.
    /// </summary>
    public const string TreePrefix = "Tree.";
    /// <summary>
    /// Prefix of a tree context menu entry's automation id: the command id follows.
    /// </summary>
    public const string TreeMenuPrefix = "Tree.Menu.";
    /// <summary>
    /// Tree item kind of the project.
    /// </summary>
    public const string TreeKindProject = "project";
    /// <summary>
    /// Tree item kind of a class.
    /// </summary>
    public const string TreeKindClass = "class";
    /// <summary>
    /// Tree item kind of a group of a class (Methods, Constructors, Variables, Event graphs).
    /// </summary>
    public const string TreeKindGroup = "group";
    /// <summary>
    /// Tree item kind of a method.
    /// </summary>
    public const string TreeKindMethod = "method";
    /// <summary>
    /// Tree item kind of a constructor.
    /// </summary>
    public const string TreeKindConstructor = "constructor";
    /// <summary>
    /// Tree item kind of a variable.
    /// </summary>
    public const string TreeKindVariable = "variable";
    /// <summary>
    /// Tree item kind of an event graph.
    /// </summary>
    public const string TreeKindEventGraph = "eventgraph";
    /// <summary>
    /// Automation id for the inspector panel's hosted content.
    /// </summary>
    public const string InspectorContent = "Inspector.Content";
    /// <summary>
    /// Automation id for the inspector panel's empty-state text.
    /// </summary>
    public const string InspectorEmpty = "Inspector.Empty";
    /// <summary>
    /// Automation id for the Errors panel's list of rows.
    /// </summary>
    public const string ErrorsList = "Errors.List";
    /// <summary>
    /// Automation id for the Errors panel's empty-state text.
    /// </summary>
    public const string ErrorsEmpty = "Errors.Empty";
    /// <summary>
    /// Automation id for the Output panel's list of lines.
    /// </summary>
    public const string OutputLines = "Output.Lines";
    /// <summary>
    /// Automation id for the C# panel's code view.
    /// </summary>
    public const string CSharpCode = "CSharp.Code";
    /// <summary>
    /// Automation id for the C# panel's empty-state text.
    /// </summary>
    public const string CSharpEmpty = "CSharp.Empty";
    /// <summary>
    /// Automation id for a row of the Errors panel.
    /// </summary>
    public const string ErrorsRow = "Errors.Row";
    /// <summary>
    /// Automation id for a line of the Output panel.
    /// </summary>
    public const string OutputLine = "Output.Line";
}
