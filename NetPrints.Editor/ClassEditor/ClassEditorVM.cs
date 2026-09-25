using System.Reactive.Linq;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Translator;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Main;
using NetPrints.Editor.ModelSync;
using NetPrints.Editor.UndoRedo;
using NetPrints.Editor.Variables;

namespace NetPrints.Editor.ClassEditor;

/// <summary>Which inspector the class editor shows on the right (PAR-33).</summary>
public enum InspectorKind
{
    Class,
    Variable,
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

    private readonly ClassTranslator classTranslator = new();

    private readonly HashSet<Variable> subscribedVariables = [];
    private IDisposable? generatedCodeLoop;

    public ClassEditorVM(ClassGraph cls, EditorContext context)
    {
        Class = cls;
        Context = context;
        Messenger = context.CreateMessenger();
        Messenger.Register(this);

        Methods = new ObservableViewModelCollection<MethodVM, MethodGraph>(cls.Methods, m => new MethodVM(m));
        Constructors = new ObservableViewModelCollection<MethodVM, ConstructorGraph>(cls.Constructors, c => new MethodVM(c));
        Variables = new ObservableViewModelCollection<MemberVariableVM, Variable>(cls.Variables,
            v => new MemberVariableVM(v, this), v => v.Dispose());

        cls.Variables.CollectionChanged += OnMembersChanged;
        cls.Methods.CollectionChanged += OnMembersChanged;
        cls.Constructors.CollectionChanged += OnMembersChanged;
        SyncVariableSubscriptions();

        UndoRedo.Changed += (_, _) =>
        {
            UndoCommand.NotifyCanExecuteChanged();
            RedoCommand.NotifyCanExecuteChanged();
        };

        context.Reflection.Reloaded += OnReflectionReloaded;
        RefreshOverridableMethods();
        RefreshGeneratedCode();
    }

    public ClassGraph Class { get; }

    public EditorContext Context { get; }

    /// <summary>Messenger scoped to this class editor.</summary>
    public IMessenger Messenger { get; }

    /// <summary>Undo/redo history of this class editor.</summary>
    public UndoRedoStack UndoRedo { get; } = new();

    public Project? Project => Class.Project;

    public ObservableViewModelCollection<MethodVM, MethodGraph> Methods { get; }

    public ObservableViewModelCollection<MethodVM, ConstructorGraph> Constructors { get; }

    public ObservableViewModelCollection<MemberVariableVM, Variable> Variables { get; }

    /// <summary>The graph shown in the canvas, or null.</summary>
    [ObservableProperty]
    public partial NodeGraphVM? OpenedGraph { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowClassInspector), nameof(ShowVariableInspector), nameof(ShowMethodInspector))]
    public partial InspectorKind Inspector { get; set; } = InspectorKind.Class;

    public bool ShowClassInspector => Inspector == InspectorKind.Class;

    public bool ShowVariableInspector => Inspector == InspectorKind.Variable && SelectedVariable is not null;

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

    /// <summary>Methods of the base types that can be overridden (PAR-26).</summary>
    [ObservableProperty]
    public partial IReadOnlyList<MethodSpecifier> OverridableMethods { get; set; } = [];

    /// <summary>Selection of the "Override a method" chooser; resets after use (PAR-26).</summary>
    [ObservableProperty]
    public partial MethodSpecifier? SelectedOverride { get; set; }

    /// <summary>Window title (PAR-22).</summary>
    public string Title => Class.Name ?? "";

    public IReadOnlyList<MemberVisibility> PossibleVisibilities => Visibilities;

    public string Name
    {
        get => Class.Name;
        set
        {
            if (Class.Name != value)
            {
                Class.Name = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Title));
            }
        }
    }

    public string Namespace
    {
        get => Class.Namespace;
        set
        {
            if (Class.Namespace != value)
            {
                Class.Namespace = value;
                OnPropertyChanged();
            }
        }
    }

    public MemberVisibility Visibility
    {
        get => Class.Visibility;
        set
        {
            if (Class.Visibility != value)
            {
                Class.Visibility = value;
                OnPropertyChanged();
            }
        }
    }

    public ClassModifiers Modifiers
    {
        get => Class.Modifiers;
        set
        {
            if (Class.Modifiers != value)
            {
                Class.Modifiers = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsSealed));
                OnPropertyChanged(nameof(IsAbstract));
                OnPropertyChanged(nameof(IsStatic));
                OnPropertyChanged(nameof(IsPartial));
            }
        }
    }

    public bool IsSealed { get => Modifiers.HasFlag(ClassModifiers.Sealed); set => SetModifier(ClassModifiers.Sealed, value); }

    public bool IsAbstract { get => Modifiers.HasFlag(ClassModifiers.Abstract); set => SetModifier(ClassModifiers.Abstract, value); }

    public bool IsStatic { get => Modifiers.HasFlag(ClassModifiers.Static); set => SetModifier(ClassModifiers.Static, value); }

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
        DropDetachedState();
    }

    private void OnVariablePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Variable.GetterMethod) or nameof(Variable.SetterMethod))
        {
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
        || Class.Methods.Contains(graph as MethodGraph)
        || Class.Constructors.Contains(graph as ConstructorGraph)
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

    /// <summary>Translates the class to C# now (the loop calls this about every second).</summary>
    public void RefreshGeneratedCode()
    {
        string code;
        try
        {
            code = classTranslator.TranslateClass(Class);
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
    /// about once a second; translating a snapshot off the UI thread is a P3 performance item.
    /// </remarks>
    public void StartGeneratedCodeLoop()
    {
        // On the context's scheduler, so tests drive it in virtual time.
        generatedCodeLoop ??= Observable.Interval(TimeSpan.FromSeconds(1), Context.Scheduler)
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

    /// <summary>Saves the whole project (not just the class).</summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        if (Project is null)
        {
            return;
        }

        try
        {
            Project.Save();
        }
        catch (Exception ex)
        {
            await Context.Dialogs.ShowErrorAsync("Failed to save project", ex.ToString());
        }
    }

    [RelayCommand]
    private void Compile()
    {
        if (Project is { CanCompile: true })
        {
            Project.CompileProject();
        }
    }

    [RelayCommand]
    private Task RunAsync() =>
        Project is { CanCompileAndRun: true } ? MainEditorVM.CompileAndRunAsync(Project, Context) : Task.CompletedTask;

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

    public void Dispose()
    {
        generatedCodeLoop?.Dispose();

        Context.Reflection.Reloaded -= OnReflectionReloaded;
        Class.Variables.CollectionChanged -= OnMembersChanged;
        Class.Methods.CollectionChanged -= OnMembersChanged;
        Class.Constructors.CollectionChanged -= OnMembersChanged;
        foreach (var variable in subscribedVariables)
        {
            ((INotifyPropertyChanged)variable).PropertyChanged -= OnVariablePropertyChanged;
        }

        subscribedVariables.Clear();
        Messenger.UnregisterAll(this);
        OpenedGraph = null;
        Methods.Dispose();
        Constructors.Dispose();
        Variables.Dispose();
    }
}
