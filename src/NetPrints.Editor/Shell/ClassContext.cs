using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using NetPrints.Core;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.CodeView;
using NetPrints.Editor.Events;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Inspectors;
using NetPrints.Editor.ModelSync;
using NetPrints.Editor.UndoRedo;
using NetPrints.Editor.Variables;
using NetPrints.Graph;

namespace NetPrints.Editor.Shell;

/// <summary>
/// What the shell works on for one class, owned by <see cref="ProjectSessionViewModel"/>: the class's undo stack, the
/// services graph documents are built on, the view models of its members and class inspector, and the dirty tracking of
/// every edit to the class (editor-services.md §3).
/// </summary>
public sealed class ClassContext : IDisposable
{
    /// <summary>Grid cells from the origin to a newly created member's entry node.</summary>
    private const double NewMemberEntryGridOffset = 4;

    /// <summary>Grid cells from a newly created method's entry node to its return node.</summary>
    private const double NewMethodReturnGridOffset = 15;

    private readonly HashSet<Variable> subscribedVariables = [];
    private readonly HashSet<ExecutionGraph> subscribedMethods = [];
    private readonly HashSet<NodeGraph> dirtyTrackedGraphs = [];
    private readonly HashSet<Node> dirtyTrackedNodes = [];
    private readonly HashSet<NodePin> dirtyTrackedPins = [];
    private readonly HashSet<INotifyCollectionChanged> dirtyTrackedPinCollections = [];

    /// <summary>Creates the context of <paramref name="cls"/>.</summary>
    /// <param name="cls">The class.</param>
    /// <param name="context">Host services shared across the editor.</param>
    /// <param name="undoRedo">The class's undo stack; every applied command marks the class dirty.</param>
    public ClassContext(ClassGraph cls, EditorContext context, UndoRedoStack undoRedo)
    {
        ArgumentNullException.ThrowIfNull(cls);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(undoRedo);
        Class = cls;
        Context = context;
        UndoRedo = undoRedo;
        Messenger = context.CreateMessenger();
        Services = new ClassEditorServices(context, undoRedo, Messenger);

        Methods = new ObservableViewModelCollection<MethodViewModel, MethodGraph>(cls.Methods, m => new MethodViewModel(m), m => m.Dispose());
        Constructors = new ObservableViewModelCollection<MethodViewModel, ConstructorGraph>(cls.Constructors, c => new MethodViewModel(c), m => m.Dispose());
        Variables = new ObservableViewModelCollection<MemberVariableViewModel, Variable>(cls.Variables,
            v => new MemberVariableViewModel(v, Services), v => v.Dispose());
        EventGraphs = new ObservableViewModelCollection<EventGraphViewModel, EventGraph>(cls.EventGraphs, g => new EventGraphViewModel(g, cls));
        CodeView = new CodeViewViewModel(cls, context.CodeAnalysis);
        ClassInspector = new ClassInspectorViewModel(this);

        cls.Variables.CollectionChanged += OnMembersChanged;
        cls.Methods.CollectionChanged += OnMembersChanged;
        cls.Constructors.CollectionChanged += OnMembersChanged;
        cls.EventGraphs.CollectionChanged += OnMembersChanged;
        SyncVariableSubscriptions();
        SyncMethodSubscriptions();
        SyncDirtyTrackingGraphs();

        // Every applied undo/redo command edits the model.
        UndoRedo.Applied += OnUndoApplied;
        RequestCodeAnalysis();
    }

    /// <summary>Raised after the class's members or a variable's accessor graphs changed.</summary>
    public event EventHandler? MembersChanged;

    /// <summary>Gets the class.</summary>
    public ClassGraph Class { get; }

    /// <summary>Gets the host services shared across the editor.</summary>
    public EditorContext Context { get; }

    /// <summary>Gets the messenger scoped to the class.</summary>
    public IMessenger Messenger { get; }

    /// <summary>Gets the undo stack of the class.</summary>
    public UndoRedoStack UndoRedo { get; }

    /// <summary>Gets the services the class's graph and member view models depend on.</summary>
    public ClassEditorServices Services { get; }

    /// <summary>Gets the view models of the class's methods.</summary>
    public ObservableViewModelCollection<MethodViewModel, MethodGraph> Methods { get; }

    /// <summary>Gets the view models of the class's constructors.</summary>
    public ObservableViewModelCollection<MethodViewModel, ConstructorGraph> Constructors { get; }

    /// <summary>Gets the view models of the class's variables.</summary>
    public ObservableViewModelCollection<MemberVariableViewModel, Variable> Variables { get; }

    /// <summary>Gets the view models of the class's event graphs.</summary>
    public ObservableViewModelCollection<EventGraphViewModel, EventGraph> EventGraphs { get; }

    /// <summary>Gets the read-only C# code view of the class inspector.</summary>
    public CodeViewViewModel CodeView { get; }

    /// <summary>Gets the view model of the class inspector.</summary>
    public ClassInspectorViewModel ClassInspector { get; }

    /// <summary>Gets whether <see cref="Dispose"/> ran.</summary>
    public bool IsDisposed { get; private set; }

    /// <summary>Creates a method named Method, Method1, ... with connected entry and return nodes.</summary>
    /// <returns>The method.</returns>
    public MethodGraph CreateMethod()
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
        return method;
    }

    /// <summary>Creates a public constructor.</summary>
    /// <returns>The constructor.</returns>
    public ConstructorGraph CreateConstructor()
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
        return constructor;
    }

    /// <summary>Creates an override of a base method.</summary>
    /// <param name="methodSpecifier">The method to override.</param>
    /// <returns>The method, or null when it cannot be overridden.</returns>
    public MethodGraph? CreateOverride(MethodSpecifier methodSpecifier) => GraphUtil.AddOverrideMethod(Class, methodSpecifier);

    /// <summary>Creates a variable named Variable, Variable1, ... of type object (undoable).</summary>
    public void CreateVariable()
    {
        string name = NetPrintsUtil.GetUniqueName("Variable", Class.Variables.Select(v => v.Name).ToList());
        UndoRedo.Do(EditorCommands.AddVariable(Class, name));
    }

    /// <summary>Creates an event graph named EventGraph, EventGraph1, ... (undoable).</summary>
    /// <returns>The event graph.</returns>
    public EventGraph CreateEventGraph()
    {
        string name = NetPrintsUtil.GetUniqueName(EventGraph.DefaultNamePrefix, Class.EventGraphs.Select(g => g.Name).ToList());
        var eventGraph = new EventGraph(name) { Class = Class };
        UndoRedo.Do(EditorCommands.AddEventGraph(Class, eventGraph));
        return eventGraph;
    }

    /// <summary>Removes a method or constructor (undoable).</summary>
    /// <param name="graph">The method or constructor of the class.</param>
    public void RemoveMethod(ExecutionGraph graph) => UndoRedo.Do(EditorCommands.RemoveMethod(Class, graph));

    /// <summary>Removes an event graph (undoable).</summary>
    /// <param name="graph">The event graph of the class.</param>
    public void RemoveEventGraph(EventGraph graph) => UndoRedo.Do(EditorCommands.RemoveEventGraph(Class, graph));

    /// <summary>Unsubscribes from every model event and disposes the member view models.</summary>
    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        IsDisposed = true;
        UndoRedo.Applied -= OnUndoApplied;
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
        CodeView.Dispose();
        Methods.Dispose();
        Constructors.Dispose();
        Variables.Dispose();
        EventGraphs.Dispose();
    }

    private void OnUndoApplied(object? sender, EventArgs e) => MarkDirty();

    private void OnMembersChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SyncVariableSubscriptions();
        SyncMethodSubscriptions();
        SyncDirtyTrackingGraphs();
        MembersChanged?.Invoke(this, EventArgs.Empty);
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
                MembersChanged?.Invoke(this, EventArgs.Empty);
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
    /// (<see cref="MethodViewModel.Name"/>, <see cref="MethodViewModel.Visibility"/>, <see cref="MethodViewModel.Modifiers"/>)
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

    /// <summary>Marks the class dirty and requests a live-analysis refresh (FR-032, SC-006): the
    /// single place every model edit that can change the generated code funnels through.</summary>
    internal void MarkDirty()
    {
        Class.MarkDirty();
        RequestCodeAnalysis();
    }

    /// <summary>Requests live analysis of the project's current classes, if the class belongs to one.</summary>
    private void RequestCodeAnalysis()
    {
        if (Class.Project is { } project)
        {
            Context.CodeAnalysis.RequestAnalysis(project);
        }
    }
}
