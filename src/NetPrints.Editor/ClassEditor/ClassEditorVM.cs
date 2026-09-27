using System.Collections.Specialized;
using System.ComponentModel;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using NetPrints.Core;
using NetPrints.Editor.Events;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Main;
using NetPrints.Editor.ModelSync;
using NetPrints.Editor.UndoRedo;
using NetPrints.Editor.Variables;
using NetPrints.Graph;
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
public sealed partial class ClassEditorVM : ObservableObject, IRecipient<OpenGraphMessage>, IDisposable
{
    internal static readonly IReadOnlyList<MemberVisibility> Visibilities =
    [
        MemberVisibility.Internal,
        MemberVisibility.Private,
        MemberVisibility.Protected,
        MemberVisibility.Public,
    ];

    private readonly HashSet<Variable> subscribedVariables = [];
    private readonly HashSet<NodeGraph> dirtyTrackedGraphs = [];
    private readonly HashSet<Node> dirtyTrackedNodes = [];
    private readonly HashSet<NodePin> dirtyTrackedPins = [];
    private readonly HashSet<INotifyCollectionChanged> dirtyTrackedPinCollections = [];
    private IDisposable? generatedCodeLoop;

    /// <summary>Longest retained Output text, in characters (roughly 1 MB): older lines are
    /// dropped, oldest first, once exceeded.</summary>
    private const int MaxOutputChars = 1_000_000;

    private const string OutputTruncatedMarker = "… earlier output truncated …";

    private readonly Queue<string> outputLines = new();
    private readonly Subject<string> outputReceived = new();
    private int outputCharCount;
    private bool outputTruncated;
    private IDisposable? outputFlush;

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
        Messenger.Register(this);

        Methods = new ObservableViewModelCollection<MethodVM, MethodGraph>(cls.Methods, m => new MethodVM(m, cls));
        Constructors = new ObservableViewModelCollection<MethodVM, ConstructorGraph>(cls.Constructors, c => new MethodVM(c, cls));
        Variables = new ObservableViewModelCollection<MemberVariableVM, Variable>(cls.Variables,
            v => new MemberVariableVM(v, this), v => v.Dispose());
        EventGraphs = new ObservableViewModelCollection<EventGraphVM, EventGraph>(cls.EventGraphs, g => new EventGraphVM(g, cls));

        cls.Variables.CollectionChanged += OnMembersChanged;
        cls.Methods.CollectionChanged += OnMembersChanged;
        cls.Constructors.CollectionChanged += OnMembersChanged;
        cls.EventGraphs.CollectionChanged += OnMembersChanged;
        SyncVariableSubscriptions();
        SyncDirtyTrackingGraphs();

        UndoRedo.Changed += (_, _) =>
        {
            UndoCommand.NotifyCanExecuteChanged();
            RedoCommand.NotifyCanExecuteChanged();
        };

        // Dirty tracking (editor-services.md §3, data-model.md §2): every applied undo/redo command
        // edits the model.
        UndoRedo.Applied += (_, _) => Class.MarkDirty();

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
        RefreshGeneratedCode();
    }

    /// <summary>The wrapped model class.</summary>
    public ClassGraph Class { get; }

    /// <summary>Host services shared across the editor.</summary>
    public EditorContext Context { get; }

    /// <summary>Messenger scoped to this class editor.</summary>
    public IMessenger Messenger { get; }

    /// <summary>Undo/redo history of this class editor.</summary>
    public UndoRedoStack UndoRedo { get; } = new();

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

    /// <summary>Generated C# of the class, refreshed about every second (PAR-34).</summary>
    [ObservableProperty]
    public partial string GeneratedCode { get; set; } = "";

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
                Class.MarkDirty();
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
                Class.MarkDirty();
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
                Class.MarkDirty();
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
                Class.MarkDirty();
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

    partial void OnOpenedGraphChanged(NodeGraphVM? oldValue, NodeGraphVM? newValue) => oldValue?.Dispose();

    /// <summary>Opens a graph in the canvas.</summary>
    public void OpenGraph(NodeGraph graph) => OpenedGraph = new NodeGraphVM(graph, this);

    void IRecipient<OpenGraphMessage>.Receive(OpenGraphMessage message) => OpenGraph(message.Graph);

    // Model changes, including undo and redo, can remove what the inspector or the canvas shows.
    // The editor reacts to the model instead of each command cleaning up after itself.

    private void OnMembersChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SyncVariableSubscriptions();
        SyncDirtyTrackingGraphs();
        DropDetachedState();
    }

    private void OnVariablePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Variable.GetterMethod) or nameof(Variable.SetterMethod))
        {
            SyncDirtyTrackingGraphs();
            DropDetachedState();
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

    /// <summary>Graphs that belong to the class: its own graph, methods, constructors and variable graphs.</summary>
    private bool BelongsToClass(NodeGraph graph) =>
        graph == Class
        || (graph is MethodGraph method && Class.Methods.Contains(method))
        || (graph is ConstructorGraph constructor && Class.Constructors.Contains(constructor))
        || (graph is EventGraph eventGraph && Class.EventGraphs.Contains(eventGraph))
        || Class.Variables.Any(v => v.GetterMethod == graph || v.SetterMethod == graph || v.TypeGraph == graph);

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
        Class.MarkDirty();
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
        Class.MarkDirty();
    }

    private void OnDirtyTrackedPinPropertyChanged(object? sender, PropertyChangedEventArgs e) => Class.MarkDirty();

    private void OnDirtyTrackedNodePositionChanged(Node node, double positionX, double positionY) => Class.MarkDirty();

    private ClassTranslator NewTranslator() => new(Context.Extensions.Current.Translation);

    /// <summary>Translates the class to C# now (the loop calls this about every second).</summary>
    public void RefreshGeneratedCode()
    {
        string code;
        try
        {
            code = NewTranslator().TranslateClass(Class);
        }
        catch (Exception ex)
        {
            code = ex.ToString();
        }

        GeneratedCode = code;
    }

    /// <summary>
    /// Starts refreshing <see cref="GeneratedCode"/> about every second until the editor is disposed.
    /// Replaces the WPF editor's timer thread + dispatcher.
    /// </summary>
    /// <remarks>
    /// The translation runs on the UI thread on purpose: the model is not thread-safe, and the WPF
    /// editor's background translation raced with edits. On very large classes this can stutter
    /// about once a second; translating a snapshot off the UI thread is a P8 performance item.
    /// </remarks>
    public void StartGeneratedCodeLoop()
    {
        // On the context's code-refresh scheduler, so tests drive it in virtual time (or silence
        // it entirely, e.g. a snapshot test capturing the preview it would otherwise race).
        generatedCodeLoop ??= Observable.Interval(TimeSpan.FromSeconds(1), Context.CodeRefreshScheduler)
            .Subscribe(_ => Context.Dispatcher.Post(RefreshGeneratedCode));
    }

    // Toolbar (PAR-23)

    /// <summary>Class button: shows the class inspector and opens the class graph.</summary>
    [RelayCommand]
    private void ShowClass()
    {
        Inspector = InspectorKind.Class;
        OpenGraph(Class);
    }

    /// <summary>Saves every edited class of the whole project (not just this one) (document-format.md §2.8).</summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        if (Project is not { } project)
        {
            return;
        }

        try
        {
            await Context.Persistence.SaveAsync(project, cls => RenderGenerated(project, cls), CancellationToken.None);
        }
        catch (Exception ex)
        {
            await Context.Dialogs.ShowErrorAsync("Failed to save project", ex.ToString());
        }
    }

    /// <summary>Renders a class's generated C# file the same way a build would (project-system.md §3).</summary>
    private string RenderGenerated(Project project, ClassGraph cls) =>
        NetPrints.Generator.GraphCodeGenerator.RenderFile(NewTranslator().TranslateClass(cls), Path.GetFileName(project.GetGraphFilePath(cls)));

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

        method.EntryNode.PositionX = cell * 4;
        method.EntryNode.PositionY = cell * 4;
        method.MainReturnNode.PositionX = method.EntryNode.PositionX + cell * 15;
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

        constructor.EntryNode.PositionX = cell * 4;
        constructor.EntryNode.PositionY = cell * 4;

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

    /// <summary>Removes a variable (undoable); clears the inspector and canvas when they show it.</summary>
    public void RemoveVariable(MemberVariableVM variable) => UndoRedo.Do(EditorCommands.RemoveVariable(Class, variable.Variable));

    /// <summary>Creates an event graph named EventGraph, EventGraph1, ... (undoable, US4) and opens it.</summary>
    [RelayCommand]
    private void CreateEventGraph()
    {
        string name = NetPrintsUtil.GetUniqueName("EventGraph", Class.EventGraphs.Select(g => g.Name).ToList());
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

    /// <summary>Shows the variable inspector (PAR-29).</summary>
    public void SelectVariable(MemberVariableVM variable)
    {
        SelectedVariable = variable;
        Inspector = InspectorKind.Variable;
    }

    /// <summary>Single click on a method or constructor: shows the method inspector (PAR-24).</summary>
    [RelayCommand]
    private void SelectMethod(MethodVM? method)
    {
        if (method is null)
        {
            return;
        }

        SelectedMethod = method;
        Inspector = InspectorKind.Method;
    }

    /// <summary>Double click on a method or constructor: opens its graph (PAR-24).</summary>
    [RelayCommand]
    private void OpenMethod(MethodVM? method)
    {
        if (method is not null)
        {
            OpenGraph(method.Graph);
        }
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
    /// Stops the generated-code loop and output buffering, unsubscribes from every model and host
    /// event, clears <see cref="OpenedGraph"/>, and disposes the method/constructor/variable
    /// collections (and, through them, every member view model).
    /// </summary>
    public void Dispose()
    {
        generatedCodeLoop?.Dispose();
        outputFlush?.Dispose();
        outputReceived.Dispose();

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
        Methods.Dispose();
        Constructors.Dispose();
        Variables.Dispose();
        EventGraphs.Dispose();
    }
}
