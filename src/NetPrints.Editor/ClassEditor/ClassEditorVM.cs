using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using NetPrints.Compilation;
using NetPrints.Core;
using NetPrints.Editor.CodeView;
using NetPrints.Editor.ErrorList;
using NetPrints.Editor.Events;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Main;
using NetPrints.Editor.ModelSync;
using NetPrints.Editor.UndoRedo;
using NetPrints.Editor.Variables;
using NetPrints.Graph;
using NetPrints.Serialization;
using NetPrints.Translator;

namespace NetPrints.Editor.ClassEditor;

/// <summary>Which inspector the class editor shows on the right (PAR-33).</summary>
public enum InspectorKind
{
    /// <summary>The class inspector (name, namespace, visibility, modifiers).</summary>
    Class,

    /// <summary>The variable inspector, for <see cref="ClassEditorVM.SelectedVariable"/>.</summary>
    Variable,

    /// <summary>The method inspector, for <see cref="ClassEditorVM.SelectedMethod"/>.</summary>
    Method,
}

/// <summary>
/// View model of a class editor window (PAR-22..37, PAR-60).
/// </summary>
public sealed partial class ClassEditorVM : ObservableObject, IRecipient<OpenGraphMessage>, IRecipient<NavigateToNodeMessage>, IRecipient<SelectInspectorMessage>, IDisposable
{
    internal static readonly IReadOnlyList<MemberVisibility> Visibilities =
    [
        MemberVisibility.Internal,
        MemberVisibility.Private,
        MemberVisibility.Protected,
        MemberVisibility.Public,
    ];

    private readonly HashSet<Variable> subscribedVariables = [];
    private readonly HashSet<ExecutionGraph> subscribedMethods = [];
    private readonly HashSet<NodeGraph> dirtyTrackedGraphs = [];
    private readonly HashSet<Node> dirtyTrackedNodes = [];
    private readonly HashSet<NodePin> dirtyTrackedPins = [];
    private readonly HashSet<INotifyCollectionChanged> dirtyTrackedPinCollections = [];

    /// <summary>Longest retained Output text, in characters (roughly 1 MB): older lines are
    /// dropped, oldest first, once exceeded.</summary>
    private const int MaxOutputChars = 1_000_000;

    private const string OutputTruncatedMarker = "… earlier output truncated …";

    /// <summary>Grid cells from the origin to a newly created member's entry node.</summary>
    private const double NewMemberEntryGridOffset = 4;

    /// <summary>Grid cells from a newly created method's entry node to its return node.</summary>
    private const double NewMethodReturnGridOffset = 15;

    /// <summary>How long a graph open must run before the busy overlay appears (batch D1):
    /// generous enough that a normal open on a small graph never flickers it.</summary>
    internal static readonly TimeSpan BusyIndicatorDelay = TimeSpan.FromMilliseconds(150);

    private readonly Queue<string> outputLines = new();
    private readonly Subject<string> outputReceived = new();
    private int outputCharCount;
    private bool outputTruncated;
    private IDisposable? outputFlush;
    private CancellationTokenSource? openGraphCts;
    private MethodVM? pendingOpenMethod;

    /// <summary>
    /// Test seam (batch D1): awaited, if set, right after the busy-indicator timer is scheduled and
    /// before the real background work starts, so a test can hold a graph open "in flight"
    /// deterministically instead of racing real <see cref="Task.Run(Action)"/> completion against the
    /// test's own next statement (a genuine flake: the real work is fast enough to finish first in a
    /// Release build). Always <see langword="null"/> in production.
    /// </summary>
    internal Func<Task>? OpenGraphDelayForTests { get; set; }

    /// <summary>
    /// Wraps <paramref name="cls"/>: builds its method/constructor/variable collections, subscribes
    /// to model and reflection-reload events, starts buffering process output, and computes the
    /// initial overridable methods and generated-code preview.
    /// </summary>
    /// <param name="cls">Class to edit.</param>
    /// <param name="context">Host services shared across the editor.</param>
    public ClassEditorVM(ClassGraph cls, EditorContext context)
    {
        Class = cls;
        Context = context;
        Messenger = context.CreateMessenger();
        // RegisterAll, not Register: this implements more than one IRecipient<T>.
        Messenger.RegisterAll(this);
        Services = new ClassEditorServices(context, UndoRedo, Messenger);

        Methods = new ObservableViewModelCollection<MethodVM, MethodGraph>(cls.Methods, m => new MethodVM(m), m => m.Dispose());
        Constructors = new ObservableViewModelCollection<MethodVM, ConstructorGraph>(cls.Constructors, c => new MethodVM(c), m => m.Dispose());
        Variables = new ObservableViewModelCollection<MemberVariableVM, Variable>(cls.Variables,
            v => new MemberVariableVM(v, Services), v => v.Dispose());
        EventGraphs = new ObservableViewModelCollection<EventGraphVM, EventGraph>(cls.EventGraphs, g => new EventGraphVM(g, cls));
        VariablesPanel = new VariablesPanelVM(Services, Variables);
        CodeView = new CodeViewVM(cls, context.CodeAnalysis);
        ErrorList = new ErrorListVM(cls, context.CodeAnalysis, Messenger);

        cls.Variables.CollectionChanged += OnMembersChanged;
        cls.Methods.CollectionChanged += OnMembersChanged;
        cls.Constructors.CollectionChanged += OnMembersChanged;
        cls.EventGraphs.CollectionChanged += OnMembersChanged;
        SyncVariableSubscriptions();
        SyncMethodSubscriptions();
        SyncDirtyTrackingGraphs();

        UndoRedo.Changed += (_, _) =>
        {
            UndoCommand.NotifyCanExecuteChanged();
            RedoCommand.NotifyCanExecuteChanged();
        };

        // Dirty tracking (editor-services.md §3, data-model.md §2): every applied undo/redo command
        // edits the model.
        UndoRedo.Applied += (_, _) => MarkDirty();

        context.Reflection.Reloaded += OnReflectionReloaded;
        context.Processes.OutputReceived += OnProcessOutputReceived;

        // Coalesced: a chatty program (e.g. Console.WriteLine in a loop) would otherwise post one
        // dispatcher operation and rebuild the whole Output string per line, which is O(n^2) in the
        // total output and floods the UI thread.
        outputFlush = outputReceived
            .Buffer(TimeSpan.FromMilliseconds(50), Context.Scheduler)
            .Where(batch => batch.Count > 0)
            .Subscribe(batch => Context.Dispatcher.Post(() => AppendOutput(batch)));

        RefreshOverridableMethods();
        RequestCodeAnalysis();
    }

    /// <summary>The wrapped model class.</summary>
    public ClassGraph Class { get; }

    /// <summary>Host services shared across the editor.</summary>
    public EditorContext Context { get; }

    /// <summary>Messenger scoped to this class editor.</summary>
    public IMessenger Messenger { get; }

    /// <summary>Undo/redo history of this class editor.</summary>
    public UndoRedoStack UndoRedo { get; } = new();

    /// <summary>Narrow services shared with child view models that must not depend on this class editor directly (FR-038).</summary>
    public ClassEditorServices Services { get; }

    /// <summary>The project the class belongs to, or <see langword="null"/> if it has not been added to one.</summary>
    public Project? Project => Class.Project;

    /// <summary>View models for <see cref="Class"/>'s methods.</summary>
    public ObservableViewModelCollection<MethodVM, MethodGraph> Methods { get; }

    /// <summary>View models for <see cref="Class"/>'s constructors.</summary>
    public ObservableViewModelCollection<MethodVM, ConstructorGraph> Constructors { get; }

    /// <summary>View models for <see cref="Class"/>'s variables.</summary>
    public ObservableViewModelCollection<MemberVariableVM, Variable> Variables { get; }

    /// <summary>View models for <see cref="Class"/>'s event graphs (US4).</summary>
    public ObservableViewModelCollection<EventGraphVM, EventGraph> EventGraphs { get; }

    /// <summary>The Variables panel's "Class" and "Method: &lt;name&gt;" groups (FR-030, US5).</summary>
    public VariablesPanelVM VariablesPanel { get; }

    /// <summary>The read-only C# code view of the class inspector (US6, FR-031..035).</summary>
    public CodeViewVM CodeView { get; }

    /// <summary>The class editor's Errors tab (US6, FR-032, FR-034).</summary>
    public ErrorListVM ErrorList { get; }

    /// <summary>The graph shown in the canvas, or null.</summary>
    [ObservableProperty]
    public partial NodeGraphVM? OpenedGraph { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowClassInspector), nameof(ShowVariableInspector), nameof(ShowMethodInspector))]
    public partial InspectorKind Inspector { get; set; } = InspectorKind.Class;

    /// <summary>Whether the class inspector should be shown.</summary>
    public bool ShowClassInspector => Inspector == InspectorKind.Class;

    /// <summary>Whether the variable inspector should be shown (a variable is also selected).</summary>
    public bool ShowVariableInspector => Inspector == InspectorKind.Variable && SelectedVariable is not null;

    /// <summary>Whether the method inspector should be shown (a method is also selected).</summary>
    public bool ShowMethodInspector => Inspector == InspectorKind.Method && SelectedMethod is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowVariableInspector))]
    public partial MemberVariableVM? SelectedVariable { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowMethodInspector))]
    public partial MethodVM? SelectedMethod { get; set; }

    /// <summary>Whether a graph is still opening past <see cref="BusyIndicatorDelay"/> (batch D1):
    /// drives the "Opening &lt;name&gt;…" overlay. Never true for an open that finishes quickly.</summary>
    [ObservableProperty]
    public partial bool IsOpeningGraph { get; set; }

    /// <summary>Name shown by the busy overlay while <see cref="IsOpeningGraph"/> is true.</summary>
    [ObservableProperty]
    public partial string? OpeningGraphName { get; set; }

    /// <summary>
    /// stdout/stderr of every program Run has started (PAR-10), across every open class window
    /// (they all show the same log): the Output tab, so the console output is visible on every
    /// platform instead of only on the editor's own terminal.
    /// </summary>
    [ObservableProperty]
    public partial string Output { get; set; } = "";

    /// <summary>Index of the selected tab in the errors/output panel; switches to Output when a run starts.</summary>
    [ObservableProperty]
    public partial int SelectedBottomTab { get; set; }

    /// <summary>Methods of the base types that can be overridden (PAR-26).</summary>
    [ObservableProperty]
    public partial IReadOnlyList<MethodSpecifier> OverridableMethods { get; set; } = [];

    /// <summary>Selection of the "Override a method" chooser; resets after use (PAR-26).</summary>
    [ObservableProperty]
    public partial MethodSpecifier? SelectedOverride { get; set; }

    /// <summary>Window title (PAR-22).</summary>
    public string Title => Class.Name ?? "";

    /// <summary>The class name with its namespace (the window's automation name).</summary>
    public string FullName => Class.FullName ?? "";

    /// <summary>The visibility values offered by the class's visibility chooser.</summary>
    public IReadOnlyList<MemberVisibility> PossibleVisibilities => Visibilities;

    /// <summary>The class's name, without namespace. Setting it also refreshes <see cref="Title"/> and <see cref="FullName"/>.</summary>
    public string Name
    {
        get => Class.Name;
        set
        {
            if (Class.Name != value)
            {
                Class.Name = value;
                MarkDirty();
                OnPropertyChanged();
                OnPropertyChanged(nameof(Title));
                OnPropertyChanged(nameof(FullName));
            }
        }
    }

    /// <summary>The class's namespace. Setting it also refreshes <see cref="FullName"/>.</summary>
    public string Namespace
    {
        get => Class.Namespace;
        set
        {
            if (Class.Namespace != value)
            {
                Class.Namespace = value;
                MarkDirty();
                OnPropertyChanged();
                OnPropertyChanged(nameof(FullName));
            }
        }
    }

    /// <summary>The class's visibility.</summary>
    public MemberVisibility Visibility
    {
        get => Class.Visibility;
        set
        {
            if (Class.Visibility != value)
            {
                Class.Visibility = value;
                MarkDirty();
                OnPropertyChanged();
            }
        }
    }

    /// <summary>The class's modifiers.</summary>
    public ClassModifiers Modifiers
    {
        get => Class.Modifiers;
        set
        {
            if (Class.Modifiers != value)
            {
                Class.Modifiers = value;
                MarkDirty();
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsSealed));
                OnPropertyChanged(nameof(IsAbstract));
                OnPropertyChanged(nameof(IsStatic));
                OnPropertyChanged(nameof(IsPartial));
            }
        }
    }

    /// <summary>Whether <see cref="ClassModifiers.Sealed"/> is set.</summary>
    public bool IsSealed { get => Modifiers.HasFlag(ClassModifiers.Sealed); set => SetModifier(ClassModifiers.Sealed, value); }

    /// <summary>Whether <see cref="ClassModifiers.Abstract"/> is set.</summary>
    public bool IsAbstract { get => Modifiers.HasFlag(ClassModifiers.Abstract); set => SetModifier(ClassModifiers.Abstract, value); }

    /// <summary>Whether <see cref="ClassModifiers.Static"/> is set.</summary>
    public bool IsStatic { get => Modifiers.HasFlag(ClassModifiers.Static); set => SetModifier(ClassModifiers.Static, value); }

    /// <summary>Whether <see cref="ClassModifiers.Partial"/> is set.</summary>
    public bool IsPartial { get => Modifiers.HasFlag(ClassModifiers.Partial); set => SetModifier(ClassModifiers.Partial, value); }

    private void SetModifier(ClassModifiers flag, bool value) => Modifiers = value ? Modifiers | flag : Modifiers & ~flag;

    private void OnReflectionReloaded(object? sender, EventArgs e) => RefreshOverridableMethods();

    private void RefreshOverridableMethods()
    {
        // Filled on Reloaded once the host has loaded.
        OverridableMethods = Context.Reflection.IsLoaded
            ? Class.AllBaseTypes.SelectMany(Context.Reflection.Provider.GetOverridableMethodsForType).ToList()
            : [];
    }

    partial void OnSelectedOverrideChanged(MethodSpecifier? value)
    {
        if (value is null)
        {
            return;
        }

        CreateOverride(value);

        // Reset the chooser after the selection change has been processed by the view.
        Context.Dispatcher.Post(() => SelectedOverride = null);
    }

    partial void OnOpenedGraphChanged(NodeGraphVM? oldValue, NodeGraphVM? newValue)
    {
        oldValue?.Dispose();
        VariablesPanel.OnOpenedGraphChanged(newValue?.Graph as ExecutionGraph);
    }

    /// <summary>Opens a graph in the canvas.</summary>
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP003", Justification = "ADR-0003: OnOpenedGraphChanged (the generated property hook) disposes the old value.")]
    public void OpenGraph(NodeGraph graph) => OpenedGraph = new NodeGraphVM(graph, Services);

    void IRecipient<OpenGraphMessage>.Receive(OpenGraphMessage message) => OpenGraph(message.Graph);

    /// <summary>
    /// Opens the graph <see cref="NavigateToNodeMessage.GraphKey"/> resolves to (if not already
    /// open) and reveals <see cref="NavigateToNodeMessage.NodeId"/> (FR-034, ED-T03). Does nothing
    /// when the key does not resolve (the class changed since the diagnostic was reported).
    /// </summary>
    void IRecipient<NavigateToNodeMessage>.Receive(NavigateToNodeMessage message)
    {
        if (GraphKeys.Resolve(Class, message.GraphKey) is not { } graph)
        {
            return;
        }

        if (OpenedGraph is null || OpenedGraph.Graph != graph)
        {
            OpenGraph(graph);
        }

        OpenedGraph?.RevealNode(message.NodeId);
    }

    /// <summary>
    /// Shows the inspector for a variable or method selected from its own list entry (PAR-24, 29):
    /// <see cref="MemberVariableVM"/> sends this instead of calling back into this class editor
    /// directly (FR-038).
    /// </summary>
    void IRecipient<SelectInspectorMessage>.Receive(SelectInspectorMessage message)
    {
        switch (message.Target)
        {
            case MemberVariableVM variable:
                SelectedVariable = variable;
                Inspector = InspectorKind.Variable;
                break;
            case MethodVM method:
                SelectedMethod = method;
                Inspector = InspectorKind.Method;
                break;
        }
    }

    // Model changes, including undo and redo, can remove what the inspector or the canvas shows.
    // The editor reacts to the model instead of each command cleaning up after itself.

    private void OnMembersChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SyncVariableSubscriptions();
        SyncMethodSubscriptions();
        SyncDirtyTrackingGraphs();
        DropDetachedState();
    }

    private void OnVariablePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            // Name/Visibility/Modifiers bypass the undo stack (the variable inspector's wrapper
            // setters, editor-services.md §3): mark dirty here instead. GetterMethod/SetterMethod
            // always change through UndoRedo.Do, which already marks dirty via Applied.
            case nameof(Variable.Name):
            case nameof(Variable.Visibility):
            case nameof(Variable.Modifiers):
                MarkDirty();
                break;
            case nameof(Variable.GetterMethod):
            case nameof(Variable.SetterMethod):
                SyncDirtyTrackingGraphs();
                DropDetachedState();
                break;
        }
    }

    private void SyncVariableSubscriptions()
    {
        var current = Class.Variables.ToHashSet();
        foreach (var removed in subscribedVariables.Where(v => !current.Contains(v)).ToList())
        {
            ((INotifyPropertyChanged)removed).PropertyChanged -= OnVariablePropertyChanged;
            subscribedVariables.Remove(removed);
        }

        foreach (var added in current.Where(v => !subscribedVariables.Contains(v)))
        {
            ((INotifyPropertyChanged)added).PropertyChanged += OnVariablePropertyChanged;
            subscribedVariables.Add(added);
        }
    }

    /// <summary>
    /// Marks the class dirty when a method or constructor's inspector wrapper setter
    /// (<see cref="MethodVM.Name"/>, <see cref="MethodVM.Visibility"/>, <see cref="MethodVM.Modifiers"/>)
    /// assigns the model directly, bypassing the undo stack (editor-services.md §3).
    /// </summary>
    private void OnMethodPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MethodGraph.Name) or nameof(ExecutionGraph.Visibility) or nameof(MethodGraph.Modifiers))
        {
            MarkDirty();
        }
    }

    private void SyncMethodSubscriptions()
    {
        var current = Class.Methods.Cast<ExecutionGraph>().Concat(Class.Constructors).ToHashSet();
        foreach (var removed in subscribedMethods.Where(g => !current.Contains(g)).ToList())
        {
            ((INotifyPropertyChanged)removed).PropertyChanged -= OnMethodPropertyChanged;
            subscribedMethods.Remove(removed);
        }

        foreach (var added in current.Where(g => !subscribedMethods.Contains(g)))
        {
            ((INotifyPropertyChanged)added).PropertyChanged += OnMethodPropertyChanged;
            subscribedMethods.Add(added);
        }
    }

    /// <summary>Graphs that belong to the class: its own graph, methods, constructors and variable graphs.</summary>
    private bool BelongsToClass(NodeGraph graph) =>
        graph == Class
        || (graph is MethodGraph method && Class.Methods.Contains(method))
        || (graph is ConstructorGraph constructor && Class.Constructors.Contains(constructor))
        || (graph is EventGraph eventGraph && Class.EventGraphs.Contains(eventGraph))
        || Class.Variables.Any(v => v.GetterMethod == graph || v.SetterMethod == graph || v.TypeGraph == graph);

    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP003", Justification = "ADR-0003: OnOpenedGraphChanged (the generated property hook) disposes the old value.")]
    private void DropDetachedState()
    {
        if (SelectedVariable is not null && !Class.Variables.Contains(SelectedVariable.Variable))
        {
            SelectedVariable = null;
            if (Inspector == InspectorKind.Variable)
            {
                Inspector = InspectorKind.Class;
            }
        }

        if (SelectedMethod is not null && !BelongsToClass(SelectedMethod.Graph))
        {
            SelectedMethod = null;
            if (Inspector == InspectorKind.Method)
            {
                Inspector = InspectorKind.Class;
            }
        }

        if (OpenedGraph is not null && !BelongsToClass(OpenedGraph.Graph))
        {
            OpenedGraph = null;
        }
    }

    /// <summary>
    /// Every graph that currently belongs to <see cref="Class"/> (data-model.md §2): the class graph
    /// itself, its methods and constructors, and each variable's getter, setter and type graph.
    /// </summary>
    private IEnumerable<NodeGraph> ClassGraphs()
    {
        yield return Class;

        foreach (var method in Class.Methods)
        {
            yield return method;
        }

        foreach (var constructor in Class.Constructors)
        {
            yield return constructor;
        }

        foreach (var eventGraph in Class.EventGraphs)
        {
            yield return eventGraph;
        }

        foreach (var variable in Class.Variables)
        {
            if (variable.GetterMethod is { } getter)
            {
                yield return getter;
            }

            if (variable.SetterMethod is { } setter)
            {
                yield return setter;
            }

            yield return variable.TypeGraph;
        }
    }

    /// <summary>
    /// Dirty tracking (editor-services.md §3): resyncs which graphs' <c>Nodes</c> collections (and,
    /// through <see cref="SyncDirtyTrackingNodes"/>, which nodes' <see cref="Node.OnPositionChanged"/>)
    /// are subscribed, following <see cref="ClassGraphs"/> as members and getter/setter graphs come
    /// and go.
    /// </summary>
    private void SyncDirtyTrackingGraphs()
    {
        var current = ClassGraphs().ToHashSet();

        foreach (var removed in dirtyTrackedGraphs.Where(g => !current.Contains(g)).ToList())
        {
            removed.Nodes.CollectionChanged -= OnDirtyTrackedGraphNodesChanged;
            dirtyTrackedGraphs.Remove(removed);
        }

        foreach (var added in current.Where(g => !dirtyTrackedGraphs.Contains(g)))
        {
            added.Nodes.CollectionChanged += OnDirtyTrackedGraphNodesChanged;
            dirtyTrackedGraphs.Add(added);
        }

        SyncDirtyTrackingNodes();
    }

    private void OnDirtyTrackedGraphNodesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SyncDirtyTrackingNodes();
        MarkDirty();
    }

    private void SyncDirtyTrackingNodes()
    {
        var nodes = dirtyTrackedGraphs.SelectMany(g => g.Nodes).ToHashSet();

        foreach (var removed in dirtyTrackedNodes.Where(n => !nodes.Contains(n)).ToList())
        {
            removed.OnPositionChanged -= OnDirtyTrackedNodePositionChanged;
            dirtyTrackedNodes.Remove(removed);
        }

        foreach (var added in nodes.Where(n => !dirtyTrackedNodes.Contains(n)))
        {
            added.OnPositionChanged += OnDirtyTrackedNodePositionChanged;
            dirtyTrackedNodes.Add(added);
        }

        SyncDirtyTrackingPins();
    }

    // Pin edits (unconnected values, connections) are model edits too: without them Compile's
    // save-all would build the stale file on disk.
    private void SyncDirtyTrackingPins()
    {
        var collections = dirtyTrackedNodes.SelectMany(NodePinCollections).ToHashSet();
        var pins = dirtyTrackedNodes.SelectMany(n => n.InputExecPins.Cast<NodePin>()
            .Concat(n.OutputExecPins).Concat(n.InputDataPins).Concat(n.OutputDataPins)
            .Concat(n.InputTypePins).Concat(n.OutputTypePins)).ToHashSet();

        foreach (var removed in dirtyTrackedPinCollections.Where(c => !collections.Contains(c)).ToList())
        {
            removed.CollectionChanged -= OnDirtyTrackedPinsChanged;
            dirtyTrackedPinCollections.Remove(removed);
        }

        foreach (var added in collections.Where(c => !dirtyTrackedPinCollections.Contains(c)))
        {
            added.CollectionChanged += OnDirtyTrackedPinsChanged;
            dirtyTrackedPinCollections.Add(added);
        }

        foreach (var removed in dirtyTrackedPins.Where(p => !pins.Contains(p)).ToList())
        {
            removed.PropertyChanged -= OnDirtyTrackedPinPropertyChanged;
            dirtyTrackedPins.Remove(removed);
        }

        foreach (var added in pins.Where(p => !dirtyTrackedPins.Contains(p)))
        {
            added.PropertyChanged += OnDirtyTrackedPinPropertyChanged;
            dirtyTrackedPins.Add(added);
        }
    }

    private static IEnumerable<INotifyCollectionChanged> NodePinCollections(Node node) =>
        [node.InputExecPins, node.OutputExecPins, node.InputDataPins, node.OutputDataPins, node.InputTypePins, node.OutputTypePins];

    private void OnDirtyTrackedPinsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SyncDirtyTrackingPins();
        MarkDirty();
    }

    private void OnDirtyTrackedPinPropertyChanged(object? sender, PropertyChangedEventArgs e) => MarkDirty();

    // Position alone never changes the generated code (the translator ignores it), so a pure pan or
    // drag only needs a save, not a re-analysis.
    private void OnDirtyTrackedNodePositionChanged(Node node, double positionX, double positionY) => Class.MarkDirty();

    private ClassTranslator NewTranslator() => new(Context.Extensions.Current.Translation);

    /// <summary>Marks the class dirty and requests a live-analysis refresh (FR-032, SC-006): the
    /// single place every model edit that can change the generated code funnels through.</summary>
    private void MarkDirty()
    {
        Class.MarkDirty();
        RequestCodeAnalysis();
    }

    /// <summary>Requests live analysis of the project's current classes, if the class belongs to one.</summary>
    private void RequestCodeAnalysis()
    {
        if (Project is { } project)
        {
            Context.CodeAnalysis.RequestAnalysis(project);
        }
    }

    // Toolbar (PAR-23)

    /// <summary>Class button: shows the class inspector and opens the class graph.</summary>
    [RelayCommand]
    private void ShowClass()
    {
        Inspector = InspectorKind.Class;
        OpenGraph(Class);
    }

    /// <summary>Saves every edited class of the whole project (not just this one) (document-format.md §2.8).
    /// A class that fails to translate is reported through <see cref="Core.Project.LastDiagnostics"/> (the
    /// Errors tab), not a dialog: its graph is still saved and the rest of the project's dirty classes
    /// still save too (R1-01).</summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        if (Project is not { } project)
        {
            return;
        }

        try
        {
            ProjectSaveResult result = await Context.Persistence.SaveAsync(project, cls => RenderGenerated(project, cls), CancellationToken.None);
            if (result.Diagnostics.Count > 0)
            {
                project.LastDiagnostics = new ObservableRangeCollection<CodeDiagnostic>(result.Diagnostics);
            }
        }
        catch (Exception ex)
        {
            await Context.Dialogs.ShowErrorAsync("Failed to save project", ex.ToString());
        }
    }

    /// <summary>Renders a class's generated C# file the same way a build would (project-system.md §3).</summary>
    private string RenderGenerated(Project project, ClassGraph cls) =>
        NetPrints.Generator.GraphCodeGenerator.RenderFile(NewTranslator().Translate(cls), Path.GetFileName(project.GetGraphFilePath(cls)));

    /// <summary>Compiles the whole project through <see cref="MainEditorVM.CompileAsync(Project, EditorContext)"/> (PAR-09).</summary>
    [RelayCommand]
    private Task CompileAsync() => Project is { CanCompile: true } project ? MainEditorVM.CompileAsync(project, Context) : Task.CompletedTask;

    [RelayCommand]
    private Task RunAsync()
    {
        if (Project is not { CanCompileAndRun: true })
        {
            return Task.CompletedTask;
        }

        SelectedBottomTab = 1; // Output, once per run (not re-forced on every line after it).
        return MainEditorVM.CompileAndRunAsync(Project, Context);
    }

    [RelayCommand]
    private void ClearOutput()
    {
        outputLines.Clear();
        outputCharCount = 0;
        outputTruncated = false;
        Output = "";
    }

    private void OnProcessOutputReceived(string line) => outputReceived.OnNext(line);

    /// <summary>
    /// Appends a batch of output lines and rebuilds <see cref="Output"/> once (not per line: O(n)
    /// in the batch, not O(n^2) in the total output), dropping the oldest lines past
    /// <see cref="MaxOutputChars"/>.
    /// </summary>
    private void AppendOutput(IList<string> lines)
    {
        foreach (string line in lines)
        {
            outputLines.Enqueue(line);
            outputCharCount += line.Length + 1;
        }

        while (outputCharCount > MaxOutputChars && outputLines.Count > 1)
        {
            outputCharCount -= outputLines.Dequeue().Length + 1;
            outputTruncated = true;
        }

        Output = outputTruncated
            ? OutputTruncatedMarker + Environment.NewLine + string.Join(Environment.NewLine, outputLines)
            : string.Join(Environment.NewLine, outputLines);
    }

    // Lists (PAR-24..30)

    /// <summary>Creates a method named Method, Method1, ... with connected entry and return nodes and opens it.</summary>
    [RelayCommand]
    private void CreateMethod()
    {
        string name = NetPrintsUtil.GetUniqueName("Method", Class.Methods.Select(m => m.Name).ToList());
        const double cell = GraphConstants.GridCellSize;

        var method = new MethodGraph(name)
        {
            Class = Class,
        };

        method.EntryNode.PositionX = cell * NewMemberEntryGridOffset;
        method.EntryNode.PositionY = cell * NewMemberEntryGridOffset;
        method.MainReturnNode.PositionX = method.EntryNode.PositionX + cell * NewMethodReturnGridOffset;
        method.MainReturnNode.PositionY = method.EntryNode.PositionY;
        GraphUtil.ConnectExecPins(method.EntryNode.InitialExecutionPin, method.MainReturnNode.ReturnPin);

        Class.Methods.Add(method);
        OpenGraph(method);
    }

    /// <summary>Creates a public constructor and opens it (PAR-28).</summary>
    [RelayCommand]
    private void CreateConstructor()
    {
        const double cell = GraphConstants.GridCellSize;
        var constructor = new ConstructorGraph()
        {
            Class = Class,
            Visibility = MemberVisibility.Public,
        };

        constructor.EntryNode.PositionX = cell * NewMemberEntryGridOffset;
        constructor.EntryNode.PositionY = cell * NewMemberEntryGridOffset;

        Class.Constructors.Add(constructor);
        OpenGraph(constructor);
    }

    /// <summary>Creates and opens an override of a base method (PAR-26).</summary>
    public void CreateOverride(MethodSpecifier methodSpecifier)
    {
        MethodGraph? method = GraphUtil.AddOverrideMethod(Class, methodSpecifier);
        if (method is not null)
        {
            OpenGraph(method);
        }
    }

    /// <summary>Creates a variable named Variable, Variable1, ... of type object (undoable, PAR-30).</summary>
    [RelayCommand]
    private void CreateVariable()
    {
        string name = NetPrintsUtil.GetUniqueName("Variable", Class.Variables.Select(v => v.Name).ToList());
        UndoRedo.Do(EditorCommands.AddVariable(Class, name));
    }

    /// <summary>Creates an event graph named EventGraph, EventGraph1, ... (undoable, US4) and opens it.</summary>
    [RelayCommand]
    private void CreateEventGraph()
    {
        string name = NetPrintsUtil.GetUniqueName(EventGraph.DefaultNamePrefix, Class.EventGraphs.Select(g => g.Name).ToList());
        var eventGraph = new EventGraph(name) { Class = Class };
        UndoRedo.Do(EditorCommands.AddEventGraph(Class, eventGraph));
        OpenGraph(eventGraph);
    }

    /// <summary>Double click on an event graph: opens it (US4).</summary>
    [RelayCommand]
    private void OpenEventGraph(EventGraphVM? eventGraph)
    {
        if (eventGraph is not null)
        {
            OpenGraph(eventGraph.Graph);
        }
    }

    /// <summary>Removes an event graph (undoable, US4); clears the canvas when it shows it.</summary>
    [RelayCommand]
    private void RemoveEventGraph(EventGraphVM? eventGraph)
    {
        if (eventGraph is null)
        {
            return;
        }

        UndoRedo.Do(EditorCommands.RemoveEventGraph(Class, eventGraph.Graph));
    }

    /// <summary>
    /// Selects and opens a method or constructor's graph (PAR-24), in one command: a single click on
    /// its list entry both shows the method inspector and opens the canvas. Previously a single click
    /// only selected it, requiring a second, discoverable double-click gesture the owner reported as
    /// "the graph doesn't open" (batch D1). Invoked directly (not through a two-way <c>SelectedItem</c>
    /// binding's changed hook): the method stays the list's <c>SelectedItem</c> across an unrelated
    /// canvas change (Create Event Graph, Class), so a changed hook would not re-fire on a second click
    /// of the same, already-selected method — exactly the "Open: switch away, then reopen" case
    /// <c>EventGraphTests</c> covers. A click on the method already loading is ignored; a click on the
    /// already-open method with nothing pending is a no-op; a click on a different method cancels the
    /// pending one. Reflection-backed overload lookups for the graph's nodes are warmed on a background
    /// thread first (AGENTS.md: heavy work off the UI thread), and <see cref="IsOpeningGraph"/> only
    /// turns on if that takes longer than <see cref="BusyIndicatorDelay"/>.
    /// </summary>
    [RelayCommand]
    private async Task OpenMethodAsync(MethodVM? method)
    {
        if (method is null)
        {
            return;
        }

        SelectedMethod = method;
        Inspector = InspectorKind.Method;

        if (method == pendingOpenMethod || (pendingOpenMethod is null && OpenedGraph?.Graph == method.Graph))
        {
            return;
        }

        pendingOpenMethod = method;
        if (openGraphCts is not null)
        {
            await openGraphCts.CancelAsync();
            openGraphCts.Dispose();
        }

        var cts = new CancellationTokenSource();
        openGraphCts = cts;
        CancellationToken token = cts.Token;

        IDisposable indicator = Context.Scheduler.Schedule(BusyIndicatorDelay, () =>
        {
            OpeningGraphName = method.Name;
            IsOpeningGraph = true;
        });

        try
        {
            if (OpenGraphDelayForTests is { } delay)
            {
                await delay();
            }

            await WarmOverloadsAsync(method.Graph, token);
            token.ThrowIfCancellationRequested();
            OpenGraph(method.Graph);
        }
        catch (OperationCanceledException)
        {
            // Superseded by a later click; OpenedGraph is untouched.
        }
        finally
        {
            indicator.Dispose();
            IsOpeningGraph = false;
            OpeningGraphName = null;
            if (pendingOpenMethod == method)
            {
                pendingOpenMethod = null;
            }
        }
    }

    /// <summary>
    /// Pre-resolves every <see cref="CallMethodNode"/>/<see cref="ConstructorNode"/> overload list of
    /// <paramref name="graph"/> on a background thread, so the reflection provider's memoized cache is
    /// already warm when <see cref="NetPrints.Editor.Graph.Nodes.NodeVM"/> recomputes the same
    /// overloads synchronously while building the canvas (AGENTS.md: heavy work off the UI thread, not
    /// papered over with a delay).
    /// </summary>
    private async Task WarmOverloadsAsync(NodeGraph graph, CancellationToken cancellationToken)
    {
        if (!Context.Reflection.IsLoaded)
        {
            return;
        }

        List<MethodSpecifier> methods = [];
        List<TypeSpecifier> constructorTypes = [];
        foreach (var node in graph.Nodes)
        {
            switch (node)
            {
                case CallMethodNode { MethodSpecifier: { } method }:
                    methods.Add(method);
                    break;
                case ConstructorNode { ConstructorSpecifier: { } ctor }:
                    constructorTypes.Add(ctor.DeclaringType);
                    break;
            }
        }

        if (methods.Count == 0 && constructorTypes.Count == 0)
        {
            return;
        }

        var provider = Context.Reflection.Provider;
        await Task.Run(() =>
        {
            foreach (var method in methods)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _ = provider.GetPublicMethodOverloads(method).Count();
            }

            foreach (var type in constructorTypes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _ = provider.GetConstructors(type).Count();
            }
        }, cancellationToken);
    }

    /// <summary>Removes a method or constructor; clears the inspector and canvas when they show it (PAR-24, 27).</summary>
    [RelayCommand]
    private void RemoveMethod(MethodVM? method)
    {
        if (method is null)
        {
            return;
        }

        UndoRedo.Do(EditorCommands.RemoveMethod(Class, method.Graph));
    }

    // Keyboard (PAR-37)

    /// <summary>Deletes the selected nodes except method entry, class return and main return nodes.</summary>
    [RelayCommand]
    private void DeleteSelectedNodes() => OpenedGraph?.DeleteSelectedNodes();

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo() => UndoRedo.Undo();

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo() => UndoRedo.Redo();

    private bool CanUndo() => UndoRedo.CanUndo;

    private bool CanRedo() => UndoRedo.CanRedo;

    /// <summary>
    /// Stops output buffering, disposes <see cref="CodeView"/> and <see cref="ErrorList"/>,
    /// unsubscribes from every model and host event, clears <see cref="OpenedGraph"/>, and disposes
    /// the method/constructor/variable collections (and, through them, every member view model).
    /// </summary>
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP003", Justification = "ADR-0003: OnOpenedGraphChanged (the generated property hook) disposes the old value.")]
    public void Dispose()
    {
        CodeView.Dispose();
        ErrorList.Dispose();
        outputFlush?.Dispose();
        outputReceived.Dispose();
        openGraphCts?.Cancel();
        openGraphCts?.Dispose();

        Context.Reflection.Reloaded -= OnReflectionReloaded;
        Context.Processes.OutputReceived -= OnProcessOutputReceived;
        Class.Variables.CollectionChanged -= OnMembersChanged;
        Class.Methods.CollectionChanged -= OnMembersChanged;
        Class.Constructors.CollectionChanged -= OnMembersChanged;
        Class.EventGraphs.CollectionChanged -= OnMembersChanged;
        foreach (var variable in subscribedVariables)
        {
            ((INotifyPropertyChanged)variable).PropertyChanged -= OnVariablePropertyChanged;
        }

        subscribedVariables.Clear();

        foreach (var method in subscribedMethods)
        {
            ((INotifyPropertyChanged)method).PropertyChanged -= OnMethodPropertyChanged;
        }

        subscribedMethods.Clear();

        foreach (var graph in dirtyTrackedGraphs)
        {
            graph.Nodes.CollectionChanged -= OnDirtyTrackedGraphNodesChanged;
        }

        dirtyTrackedGraphs.Clear();

        foreach (var node in dirtyTrackedNodes)
        {
            node.OnPositionChanged -= OnDirtyTrackedNodePositionChanged;
        }

        dirtyTrackedNodes.Clear();

        foreach (var pin in dirtyTrackedPins)
        {
            pin.PropertyChanged -= OnDirtyTrackedPinPropertyChanged;
        }

        dirtyTrackedPins.Clear();

        foreach (var collection in dirtyTrackedPinCollections)
        {
            collection.CollectionChanged -= OnDirtyTrackedPinsChanged;
        }

        dirtyTrackedPinCollections.Clear();
        Messenger.UnregisterAll(this);
        OpenedGraph = null;
        VariablesPanel.Dispose();
        Methods.Dispose();
        Constructors.Dispose();
        Variables.Dispose();
        EventGraphs.Dispose();
    }
}
