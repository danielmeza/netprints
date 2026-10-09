namespace NetPrints.Editor.Icons;

/// <summary>The built-in icon ids (FR-084). Descriptors carry one of these or an extension's own id; <see cref="IconRegistry"/> resolves them.</summary>
public static class IconIds
{
    /// <summary>The fallback glyph drawn for an id the registry does not know.</summary>
    public const string Unknown = "netprints.icon.unknown";

    /// <summary>File &gt; New project.</summary>
    public const string NewProject = "netprints.icon.newProject";

    /// <summary>File &gt; Open project.</summary>
    public const string OpenProject = "netprints.icon.openProject";

    /// <summary>File &gt; Save.</summary>
    public const string Save = "netprints.icon.save";

    /// <summary>File &gt; Save all.</summary>
    public const string SaveAll = "netprints.icon.saveAll";

    /// <summary>File &gt; Project settings.</summary>
    public const string ProjectSettings = "netprints.icon.projectSettings";

    /// <summary>File &gt; References.</summary>
    public const string References = "netprints.icon.references";

    /// <summary>File &gt; Exit.</summary>
    public const string Exit = "netprints.icon.exit";

    /// <summary>Edit &gt; Undo.</summary>
    public const string Undo = "netprints.icon.undo";

    /// <summary>Edit &gt; Redo.</summary>
    public const string Redo = "netprints.icon.redo";

    /// <summary>Edit &gt; Delete.</summary>
    public const string Delete = "netprints.icon.delete";

    /// <summary>Edit &gt; Rename.</summary>
    public const string Rename = "netprints.icon.rename";

    /// <summary>Edit &gt; Select all.</summary>
    public const string SelectAll = "netprints.icon.selectAll";

    /// <summary>Edit &gt; Add member.</summary>
    public const string AddMember = "netprints.icon.addMember";

    /// <summary>Edit &gt; Class settings.</summary>
    public const string ClassSettings = "netprints.icon.classSettings";

    /// <summary>Help &gt; Keyboard shortcuts.</summary>
    public const string KeyboardShortcuts = "netprints.icon.keyboardShortcuts";

    /// <summary>Help &gt; Start page.</summary>
    public const string Home = "netprints.icon.home";

    /// <summary>Help &gt; About.</summary>
    public const string About = "netprints.icon.about";

    /// <summary>Build &gt; Compile.</summary>
    public const string Compile = "netprints.icon.compile";

    /// <summary>Build &gt; Run.</summary>
    public const string Run = "netprints.icon.run";

    /// <summary>Build &gt; Stop.</summary>
    public const string Stop = "netprints.icon.stop";

    /// <summary>View &gt; Reset zoom.</summary>
    public const string ZoomToFit = "netprints.icon.zoomToFit";

    /// <summary>View &gt; Fit to screen.</summary>
    public const string FitToScreen = "netprints.icon.fitToScreen";

    /// <summary>View &gt; Float tab.</summary>
    public const string FloatDocument = "netprints.icon.floatDocument";

    /// <summary>View &gt; Dock tab.</summary>
    public const string DockDocument = "netprints.icon.dockDocument";

    /// <summary>View &gt; Reset layout.</summary>
    public const string ResetLayout = "netprints.icon.resetLayout";

    /// <summary>The Project panel.</summary>
    public const string PanelProjectTree = "netprints.icon.panel.projectTree";

    /// <summary>The Inspector panel.</summary>
    public const string PanelInspector = "netprints.icon.panel.inspector";

    /// <summary>The Variables panel.</summary>
    public const string PanelVariables = "netprints.icon.panel.variables";

    /// <summary>The Errors panel.</summary>
    public const string PanelErrors = "netprints.icon.panel.errors";

    /// <summary>The Output panel.</summary>
    public const string PanelOutput = "netprints.icon.panel.output";

    /// <summary>The C# panel.</summary>
    public const string PanelCSharp = "netprints.icon.panel.csharp";

    /// <summary>The console project template.</summary>
    public const string TemplateConsole = "netprints.icon.template.console";

    /// <summary>The class library project template.</summary>
    public const string TemplateLibrary = "netprints.icon.template.library";

    /// <summary>A project row; the filled glyph when active.</summary>
    public const string Project = "netprints.icon.project";

    /// <summary>A class row.</summary>
    public const string Class = "netprints.icon.class";

    /// <summary>A group row.</summary>
    public const string Group = "netprints.icon.group";

    /// <summary>A method row.</summary>
    public const string Method = "netprints.icon.method";

    /// <summary>A constructor row.</summary>
    public const string Constructor = "netprints.icon.constructor";

    /// <summary>A variable row.</summary>
    public const string Variable = "netprints.icon.variable";

    /// <summary>An event graph row.</summary>
    public const string Event = "netprints.icon.event";

    /// <summary>Add an item.</summary>
    public const string Add = "netprints.icon.add";

    /// <summary>Remove an item.</summary>
    public const string Remove = "netprints.icon.remove";

    /// <summary>Move an item up.</summary>
    public const string MoveUp = "netprints.icon.moveUp";

    /// <summary>Move an item down.</summary>
    public const string MoveDown = "netprints.icon.moveDown";

    /// <summary>Close or remove a row.</summary>
    public const string Close = "netprints.icon.close";

    /// <summary>Pin a recent project; the filled glyph when pinned.</summary>
    public const string Pin = "netprints.icon.pin";

    /// <summary>A collapsed section.</summary>
    public const string ChevronRight = "netprints.icon.chevronRight";

    /// <summary>An expanded section.</summary>
    public const string ChevronDown = "netprints.icon.chevronDown";

    /// <summary>The overloads button of a call node (T092k).</summary>
    public const string Overloads = "netprints.icon.overloads";

    /// <summary>The start page samples card.</summary>
    public const string CardSamples = "netprints.icon.card.samples";

    /// <summary>The start page learn card.</summary>
    public const string CardLearn = "netprints.icon.card.learn";

    /// <summary>The start page new project card.</summary>
    public const string CardNewProject = "netprints.icon.card.newProject";

    /// <summary>The start page open project card.</summary>
    public const string CardOpenProject = "netprints.icon.card.openProject";

    /// <summary>An error diagnostic.</summary>
    public const string SeverityError = "netprints.icon.severity.error";

    /// <summary>A warning diagnostic.</summary>
    public const string SeverityWarning = "netprints.icon.severity.warning";

    /// <summary>An information diagnostic.</summary>
    public const string SeverityInfo = "netprints.icon.severity.info";

    /// <summary>The node category icon of ConditionalRule_16x.png (T092b).</summary>
    public const string CategoryConditionalRule = "netprints.icon.category.conditionalRule";

    /// <summary>The node category icon of Convert_16x.png (T092b).</summary>
    public const string CategoryConvert = "netprints.icon.category.convert";

    /// <summary>The node category icon of Create_16x.png (T092b).</summary>
    public const string CategoryCreate = "netprints.icon.category.create";

    /// <summary>The node category icon of Delegate_16x.png (T092b).</summary>
    public const string CategoryDelegate = "netprints.icon.category.delegate";

    /// <summary>The node category icon of If_16x.png (T092b).</summary>
    public const string CategoryIf = "netprints.icon.category.if";

    /// <summary>The node category icon of ListView_16x.png (T092b).</summary>
    public const string CategoryListView = "netprints.icon.category.listView";

    /// <summary>The node category icon of Literal_16x.png (T092b).</summary>
    public const string CategoryLiteral = "netprints.icon.category.literal";

    /// <summary>The node category icon of Loop_16x.png (T092b).</summary>
    public const string CategoryLoop = "netprints.icon.category.loop";

    /// <summary>The node category icon of Method_16x.png (T092b).</summary>
    public const string CategoryMethod = "netprints.icon.category.method";

    /// <summary>The node category icon of None_16x.png (T092b).</summary>
    public const string CategoryNone = "netprints.icon.category.none";

    /// <summary>The node category icon of Operator_16x.png (T092b).</summary>
    public const string CategoryOperator = "netprints.icon.category.operator";

    /// <summary>The node category icon of Property_16x.png (T092b).</summary>
    public const string CategoryProperty = "netprints.icon.category.property";

    /// <summary>The node category icon of Return_16x.png (T092b).</summary>
    public const string CategoryReturn = "netprints.icon.category.return";

    /// <summary>The node category icon of Task_16x.png (T092b).</summary>
    public const string CategoryTask = "netprints.icon.category.task";

    /// <summary>The node category icon of Throw_16x.png (T092b).</summary>
    public const string CategoryThrow = "netprints.icon.category.throw";

    /// <summary>The node category icon of Type_16x.png (T092b).</summary>
    public const string CategoryType = "netprints.icon.category.type";

    /// <summary>NodeVisualKind.Default.</summary>
    public const string NodeKindDefault = "netprints.icon.nodeKind.default";

    /// <summary>NodeVisualKind.Entry.</summary>
    public const string NodeKindEntry = "netprints.icon.nodeKind.entry";

    /// <summary>NodeVisualKind.Return.</summary>
    public const string NodeKindReturn = "netprints.icon.nodeKind.return";

    /// <summary>NodeVisualKind.CallMethod.</summary>
    public const string NodeKindCallMethod = "netprints.icon.nodeKind.callMethod";

    /// <summary>NodeVisualKind.CallStatic.</summary>
    public const string NodeKindCallStatic = "netprints.icon.nodeKind.callStatic";

    /// <summary>NodeVisualKind.Constructor.</summary>
    public const string NodeKindConstructor = "netprints.icon.nodeKind.constructor";

    /// <summary>NodeVisualKind.MakeDelegate.</summary>
    public const string NodeKindMakeDelegate = "netprints.icon.nodeKind.makeDelegate";

    /// <summary>NodeVisualKind.Type.</summary>
    public const string NodeKindType = "netprints.icon.nodeKind.type";

    /// <summary>NodeVisualKind.VariableGetter.</summary>
    public const string NodeKindVariableGetter = "netprints.icon.nodeKind.variableGetter";

    /// <summary>NodeVisualKind.VariableSetter.</summary>
    public const string NodeKindVariableSetter = "netprints.icon.nodeKind.variableSetter";

    /// <summary>NodeVisualKind.MakeArray.</summary>
    public const string NodeKindMakeArray = "netprints.icon.nodeKind.makeArray";

    /// <summary>NodeVisualKind.Throw.</summary>
    public const string NodeKindThrow = "netprints.icon.nodeKind.throw";

    /// <summary>NodeVisualKind.Ternary.</summary>
    public const string NodeKindTernary = "netprints.icon.nodeKind.ternary";

    /// <summary>NodeVisualKind.IfElse; the SourceBranch glyph.</summary>
    public const string NodeKindIfElse = "netprints.icon.nodeKind.ifElse";

    /// <summary>NodeVisualKind.ForLoop; the Repeat glyph.</summary>
    public const string NodeKindForLoop = "netprints.icon.nodeKind.forLoop";

    /// <summary>NodeVisualKind.ExplicitCast; the SwapHorizontal glyph.</summary>
    public const string NodeKindExplicitCast = "netprints.icon.nodeKind.explicitCast";

    /// <summary>NodeVisualKind.Await; the TimerSand glyph.</summary>
    public const string NodeKindAwait = "netprints.icon.nodeKind.await";

    /// <summary>An execution pin; filled when connected.</summary>
    public const string PinExec = "netprints.icon.pin.exec";

    /// <summary>A data pin; filled when connected.</summary>
    public const string PinData = "netprints.icon.pin.data";

    /// <summary>A type pin; filled when connected.</summary>
    public const string PinType = "netprints.icon.pin.type";

    /// <summary>No project open.</summary>
    public const string EmptyProject = "netprints.icon.empty.project";

    /// <summary>No errors.</summary>
    public const string EmptyErrors = "netprints.icon.empty.errors";

    /// <summary>No output yet.</summary>
    public const string EmptyOutput = "netprints.icon.empty.output";

    /// <summary>Nothing selected.</summary>
    public const string EmptyInspector = "netprints.icon.empty.inspector";

    /// <summary>No search results.</summary>
    public const string EmptySearch = "netprints.icon.empty.search";

    /// <summary>No graph open.</summary>
    public const string EmptyGraph = "netprints.icon.empty.graph";

    /// <summary>The error dialog.</summary>
    public const string DialogError = "netprints.icon.dialog.error";

    /// <summary>A warning dialog.</summary>
    public const string DialogWarning = "netprints.icon.dialog.warning";

    /// <summary>A confirmation dialog.</summary>
    public const string DialogConfirm = "netprints.icon.dialog.confirm";

    /// <summary>An information dialog.</summary>
    public const string DialogInfo = "netprints.icon.dialog.info";

    /// <summary>The trust dialog.</summary>
    public const string DialogTrust = "netprints.icon.dialog.trust";

    /// <summary>The recovery dialog.</summary>
    public const string DialogRecover = "netprints.icon.dialog.recover";
}
