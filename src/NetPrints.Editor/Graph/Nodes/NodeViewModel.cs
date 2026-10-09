using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetPrints.Core;
using NetPrints.Editor.Controls;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Graph.Pins;
using NetPrints.Editor.ModelSync;
using NetPrints.Editor.UndoRedo;
using NetPrints.Graph;

namespace NetPrints.Editor.Graph.Nodes;

/// <summary>
/// A node of the open graph (PAR-39..42). Uses <see cref="NodeVisualKind"/> instead of brushes and
/// <see cref="GraphPoint"/> for its location.
/// </summary>
public sealed partial class NodeViewModel : ObservableObject, IDisposable
{
    private static readonly string TaskName = TypeSpecifier.FromType<Task>().Name;
    private static readonly string ValueTaskName = TypeSpecifier.FromType<ValueTask>().Name;

    private readonly INotifyPropertyChanged nodeNotifier;
    private readonly ObservableViewModelCollection<NodePinViewModel, NodeInputExecPin> inputExecPins;
    private readonly ObservableViewModelCollection<NodePinViewModel, NodeInputDataPin> inputDataPins;
    private readonly ObservableViewModelCollection<NodePinViewModel, NodeInputTypePin> inputTypePins;
    private readonly ObservableViewModelCollection<NodePinViewModel, NodeOutputExecPin> outputExecPins;
    private readonly ObservableViewModelCollection<NodePinViewModel, NodeOutputDataPin> outputDataPins;
    private readonly ObservableViewModelCollection<NodePinViewModel, NodeOutputTypePin> outputTypePins;

    /// <summary>
    /// Wraps <paramref name="node"/>: builds its pin view models, subscribes to its position, input
    /// type and property-changed events, and computes its initial overloads.
    /// </summary>
    /// <param name="node">Node to wrap.</param>
    /// <param name="graph">View model of the graph the node belongs to.</param>
    public NodeViewModel(Node node, NodeGraphViewModel graph)
    {
        Node = node;
        Graph = graph;

        inputExecPins = CreatePins(node.InputExecPins);
        inputDataPins = CreatePins(node.InputDataPins);
        inputTypePins = CreatePins(node.InputTypePins);
        outputExecPins = CreatePins(node.OutputExecPins);
        outputDataPins = CreatePins(node.OutputDataPins);
        outputTypePins = CreatePins(node.OutputTypePins);
        RebuildPinLists();

        node.OnPositionChanged += OnNodePositionChanged;
        node.InputTypeChanged += OnInputTypeChanged;
        nodeNotifier = (INotifyPropertyChanged)node;
        nodeNotifier.PropertyChanged += OnNodePropertyChanged;
        if (node.Graph is INotifyPropertyChanged graphNotifier)
        {
            graphNotifier.PropertyChanged += OnGraphPropertyChanged;
        }

        UpdateOverloads();
    }

    /// <summary>Raised when a pin was added/removed or a pin connection changed.</summary>
    public event EventHandler? PinsChanged;

    /// <summary>The wrapped model node.</summary>
    public Node Node { get; }

    /// <summary>View model of the graph this node belongs to.</summary>
    public NodeGraphViewModel Graph { get; }

    /// <summary>Input pins in display order: exec, data, type (left column).</summary>
    public ObservableCollection<NodePinViewModel> Inputs { get; } = [];

    /// <summary>Output pins in display order: exec, data, type (right column).</summary>
    public ObservableCollection<NodePinViewModel> Outputs { get; } = [];

    /// <summary>
    /// The pin area's rows (OWN-05b): for most node kinds, row i pairs <c>Inputs[i]</c> with
    /// <c>Outputs[i]</c> (identical to just rendering <see cref="Inputs"/> and <see cref="Outputs"/>
    /// as two columns). A method/event entry node and a return node instead pair each parameter's
    /// (or return value's) type pin with its data pin explicitly, so the two stay on the same row no
    /// matter how many other pins (Exec, generic type parameters) sit around them. See
    /// <see cref="BuildPinRows"/>.
    /// </summary>
    public ObservableCollection<PinRowViewModel> PinRows { get; } = [];

    /// <summary>Every pin of this node, inputs then outputs.</summary>
    public IEnumerable<NodePinViewModel> AllPins => Inputs.Concat(Outputs);

    /// <summary>View models for the wrapped node's <c>InputExecPins</c>.</summary>
    public IReadOnlyList<NodePinViewModel> InputExecPins => inputExecPins;

    /// <summary>View models for the wrapped node's <c>InputDataPins</c>.</summary>
    public IReadOnlyList<NodePinViewModel> InputDataPins => inputDataPins;

    /// <summary>View models for the wrapped node's <c>InputTypePins</c>.</summary>
    public IReadOnlyList<NodePinViewModel> InputTypePins => inputTypePins;

    /// <summary>View models for the wrapped node's <c>OutputExecPins</c>.</summary>
    public IReadOnlyList<NodePinViewModel> OutputExecPins => outputExecPins;

    /// <summary>View models for the wrapped node's <c>OutputDataPins</c>.</summary>
    public IReadOnlyList<NodePinViewModel> OutputDataPins => outputDataPins;

    /// <summary>View models for the wrapped node's <c>OutputTypePins</c>.</summary>
    public IReadOnlyList<NodePinViewModel> OutputTypePins => outputTypePins;

    /// <summary>Location on the canvas, synchronized with the model position.</summary>
    public GraphPoint Location
    {
        get => new(Node.PositionX, Node.PositionY);
        set
        {
            if (Node.PositionX != value.X || Node.PositionY != value.Y)
            {
                Node.PositionX = value.X;
                Node.PositionY = value.Y;
            }
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZIndex))]
    public partial bool IsSelected { get; set; }

    /// <summary>Selected nodes are drawn above the others.</summary>
    public int ZIndex => IsSelected ? 1 : 0;

    /// <summary>The wrapped node's name.</summary>
    public string Name => Node.Name;

    /// <summary>The wrapped node's display string (<c>Node.ToString()</c>).</summary>
    public string Label => Node.ToString();

    /// <summary>Whether the wrapped node is a <see cref="RerouteNode"/> (drawn without a body).</summary>
    public bool IsRerouteNode => Node is RerouteNode;

    /// <summary>Header color category (PAR-39).</summary>
    public NodeVisualKind VisualKind => Node switch
    {
        ExecutionEntryNode or EventEntryNode => NodeVisualKind.Entry,
        ReturnNode => NodeVisualKind.Return,
        CallMethodNode { IsStatic: true } => NodeVisualKind.CallStatic,
        CallMethodNode => NodeVisualKind.CallMethod,
        ConstructorNode => NodeVisualKind.Constructor,
        MakeDelegateNode => NodeVisualKind.MakeDelegate,
        TypeNode or MakeArrayTypeNode => NodeVisualKind.Type,
        VariableGetterNode => NodeVisualKind.VariableGetter,
        VariableSetterNode => NodeVisualKind.VariableSetter,
        MakeArrayNode => NodeVisualKind.MakeArray,
        ThrowNode => NodeVisualKind.Throw,
        TernaryNode => NodeVisualKind.Ternary,
        IfElseNode => NodeVisualKind.IfElse,
        ForLoopNode => NodeVisualKind.ForLoop,
        ExplicitCastNode => NodeVisualKind.ExplicitCast,
        AwaitNode => NodeVisualKind.Await,
        _ => NodeVisualKind.Default,
    };

    /// <summary>The role that colours the header (FR-086).</summary>
    public NodeRole Role => NodeRole.Resolve(VisualKind, Node.IsPure, Node.InputExecPins.Count + Node.OutputExecPins.Count > 0, ReturnsTask);

    /// <summary>The icon id of the glyph the header shows for <see cref="VisualKind"/> (FR-086).</summary>
    public string KindIconId => NodeIcons.For(VisualKind);

    private bool ReturnsTask =>
        Node is CallMethodNode { MethodSpecifier: { } method }
        && method.ReturnTypes.FirstOrDefault() is TypeSpecifier { Name: { } name }
        && (name == TaskName || name == ValueTaskName);

    /// <summary>Documentation tooltip for method calls (PAR-39).</summary>
    public string? ToolTip
    {
        get
        {
            var reflection = Graph.Context.Reflection;
            return Node is CallMethodNode callMethodNode && reflection.IsLoaded
                ? reflection.Provider.GetMethodDocumentation(callMethodNode.MethodSpecifier)
                : null;
        }
    }

    // Overloads (PAR-40)

    /// <summary>
    /// Gets the list the overloads button's flyout shows: every overload of a call or constructor node, or both size
    /// modes of a make-array node, the current one marked and first; <see langword="null"/> when the node has no other
    /// overload. Picking another one changes the node through the undo stack.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowOverloads))]
    public partial MethodPickerListViewModel? OverloadPicker { get; private set; }

    /// <summary>Gets the overloads button's tooltip: "Overloads (n): current signature".</summary>
    [ObservableProperty]
    public partial string OverloadsTooltip { get; private set; } = "Overloads";

    /// <summary>Gets a value indicating whether the overloads button is shown: the node has another overload.</summary>
    public bool ShowOverloads => OverloadPicker is not null;

    private void OnOverloadPicked(object? sender, MethodPickerItem item)
    {
        if (!item.IsCurrent)
        {
            Graph.ChangeOverload(this, item.Member);
        }
    }

    /// <summary>
    /// The node's current overload: its <see cref="MethodSpecifier"/> or <see cref="ConstructorSpecifier"/>
    /// for a call/constructor node, its size-mode marker for a <see cref="MakeArrayNode"/>, or
    /// <see langword="null"/> for any other node type.
    /// </summary>
    public object? CurrentOverload => ModelOperations.GetCurrentOverload(Node);

    internal void UpdateOverloads()
    {
        var reflection = Graph.Context.Reflection;
        List<MethodPickerItem> items = [];
        if (Node is MakeArrayNode makeArray)
        {
            items = [.. new[] { ModelOperations.UsePredefinedSize, ModelOperations.UseInitializerList }.Select(mode => MethodPickerItem.ForMode(mode, mode == CurrentMode(makeArray)))];
        }
        else if (reflection.IsLoaded)
        {
            // Without a loaded host the list stays empty; OnReflectionReloaded refreshes it.
            var provider = reflection.Provider;
            items = Node switch
            {
                CallMethodNode { MethodSpecifier: not null } call => CallItems(provider.GetPublicMethodOverloads(call.MethodSpecifier), call.MethodSpecifier),
                ConstructorNode { ConstructorSpecifier: not null } ctor =>
                    [.. provider.GetConstructors(ctor.ConstructorSpecifier.DeclaringType).Select(c => MethodPickerItem.For(c, SameConstructor(c, ctor.ConstructorSpecifier)))],
                _ => [],
            };
        }

        MethodPickerItem? current = items.Find(item => item.IsCurrent);
        if (items.Count < 2 || current is null)
        {
            SetOverloadPicker(null, "Overloads");
            return;
        }

        var picker = new MethodPickerListViewModel(items, showGroupHeaders: false);
        picker.Picked += OnOverloadPicked;
        SetOverloadPicker(picker, $"Overloads ({items.Count}): {current.Signature}");
    }

    private void SetOverloadPicker(MethodPickerListViewModel? picker, string tooltip)
    {
        if (OverloadPicker is { } old)
        {
            old.Picked -= OnOverloadPicked;
        }

        OverloadPicker = picker;
        OverloadsTooltip = tooltip;
    }

    private static string CurrentMode(MakeArrayNode node) =>
        node.UsePredefinedSize ? ModelOperations.UsePredefinedSize : ModelOperations.UseInitializerList;

    private static List<MethodPickerItem> CallItems(IEnumerable<MethodSpecifier> overloads, MethodSpecifier current)
    {
        List<MethodSpecifier> all = [.. overloads.Where(method => method != current).Prepend(current)];
        return [.. all.Select(method => MethodPickerItem.For(method, method == current))];
    }

    /// <summary>Refreshes everything that comes from reflection after the host (re)loaded.</summary>
    internal void OnReflectionReloaded()
    {
        UpdateOverloads();
        OnPropertyChanged(nameof(ToolTip));
        foreach (var pin in AllPins)
        {
            pin.OnReflectionReloaded();
        }
    }

    private static bool SameConstructor(ConstructorSpecifier a, ConstructorSpecifier b) =>
        a.DeclaringType == b.DeclaringType
        && a.Arguments.Select(p => p.Value).SequenceEqual(b.Arguments.Select(p => p.Value));

    // Purity (PAR-41)

    /// <summary>Whether the wrapped node's purity can be toggled from the UI.</summary>
    public bool CanSetPure => Node.CanSetPure;

    /// <summary>
    /// Whether the wrapped node is pure. Setting it only takes effect if <see cref="CanSetPure"/> is
    /// <see langword="true"/>; the setter does not go through the undo stack.
    /// </summary>
    public bool IsPure
    {
        get => Node.IsPure;
        set
        {
            if (Node.CanSetPure && Node.IsPure != value)
            {
                Node.IsPure = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Role));
            }
        }
    }

    // +/- pin buttons (PAR-42)

    /// <summary>
    /// Whether to show the left +/- pin buttons: a <see cref="MakeArrayNode"/>, <see cref="MethodEntryNode"/>,
    /// <see cref="ClassReturnNode"/>, or the graph's main <see cref="ReturnNode"/>.
    /// </summary>
    public bool ShowLeftPinButtons =>
        Node is MakeArrayNode or MethodEntryNode or ClassReturnNode
        || (Node is ReturnNode && Node == Node.MethodGraph?.MainReturnNode);

    /// <summary>Whether to show the right +/- pin buttons: a <see cref="MethodEntryNode"/> (generic parameters).</summary>
    public bool ShowRightPinButtons => Node is MethodEntryNode;

    /// <summary>Tooltip for the left "+" button, describing what it adds for this node type, or "" if <see cref="ShowLeftPinButtons"/> is <see langword="false"/>.</summary>
    public string LeftPlusToolTip => Node switch
    {
        MakeArrayNode => "Add array element",
        MethodEntryNode => "Add method parameter",
        ReturnNode => "Add method return value",
        ClassReturnNode => "Add interface",
        _ => "",
    };

    /// <summary>Tooltip for the left "-" button, describing what it removes for this node type, or "" if <see cref="ShowLeftPinButtons"/> is <see langword="false"/>.</summary>
    public string LeftMinusToolTip => Node switch
    {
        MakeArrayNode => "Remove array element",
        MethodEntryNode => "Remove method parameter",
        ReturnNode => "Remove method return value",
        ClassReturnNode => "Remove interface",
        _ => "",
    };

    /// <summary>Tooltip for the right "+" button, or "" if <see cref="ShowRightPinButtons"/> is <see langword="false"/>.</summary>
    public string RightPlusToolTip => Node is MethodEntryNode ? "Add method generic type parameter" : "";

    /// <summary>Tooltip for the right "-" button, or "" if <see cref="ShowRightPinButtons"/> is <see langword="false"/>.</summary>
    public string RightMinusToolTip => Node is MethodEntryNode ? "Remove method generic type parameter" : "";

    private void EditPins(string label, Action edit) => Graph.Services.UndoRedo.Do(EditorCommands.EditPins(Node, label, edit));

    [RelayCommand]
    private void LeftPinsPlus()
    {
        switch (Node)
        {
            case MakeArrayNode makeArrayNode:
                EditPins(LeftPlusToolTip, makeArrayNode.AddElementPin);
                break;
            case MethodEntryNode entryNode:
                EditPins(LeftPlusToolTip, entryNode.AddArgument);
                break;
            case ReturnNode returnNode:
                EditPins(LeftPlusToolTip, returnNode.AddReturnType);
                break;
            case ClassReturnNode classReturnNode:
                EditPins(LeftPlusToolTip, classReturnNode.AddInterfacePin);
                break;
        }
    }

    [RelayCommand]
    private void LeftPinsMinus()
    {
        switch (Node)
        {
            case MakeArrayNode { InputDataPins.Count: > 0 } makeArrayNode:
                EditPins(LeftMinusToolTip, () => makeArrayNode.RemoveElementPin());
                break;
            case MethodEntryNode { OutputDataPins.Count: > 0 } entryNode:
                EditPins(LeftMinusToolTip, entryNode.RemoveArgument);
                break;
            case ReturnNode { InputDataPins.Count: > 0 } returnNode:
                EditPins(LeftMinusToolTip, returnNode.RemoveReturnType);
                break;
            case ClassReturnNode classReturnNode when classReturnNode.InterfacePins.Any():
                EditPins(LeftMinusToolTip, classReturnNode.RemoveInterfacePin);
                break;
        }
    }

    [RelayCommand]
    private void RightPinsPlus()
    {
        if (Node is MethodEntryNode entryNode)
        {
            EditPins(RightPlusToolTip, entryNode.AddGenericArgument);
        }
    }

    [RelayCommand]
    private void RightPinsMinus()
    {
        if (Node is MethodEntryNode { OutputTypePins.Count: > 0 } entryNode)
        {
            EditPins(RightMinusToolTip, entryNode.RemoveGenericArgument);
        }
    }

    /// <summary>Selects only this node.</summary>
    public void Select() => Graph.SelectNodes([this], deselectPrevious: true);

    private ObservableViewModelCollection<NodePinViewModel, TPin> CreatePins<TPin>(ObservableRangeCollection<TPin> pins)
        where TPin : NodePin
    {
        var collection = new ObservableViewModelCollection<NodePinViewModel, TPin>(pins, p =>
        {
            var vm = new NodePinViewModel(p, this);
            vm.ConnectionChanged += OnPinConnectionChanged;
            return vm;
        }, vm =>
        {
            vm.ConnectionChanged -= OnPinConnectionChanged;
            vm.Dispose();
        });

        collection.CollectionChanged += OnPinCollectionChanged;
        return collection;
    }

    private void OnPinCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildPinLists();
        PinsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnPinConnectionChanged(object? sender, EventArgs e) => PinsChanged?.Invoke(this, EventArgs.Empty);

    private void RebuildPinLists()
    {
        // The pin collections are null while the constructor creates them.
        if (inputExecPins is null || inputDataPins is null || inputTypePins is null
            || outputExecPins is null || outputDataPins is null || outputTypePins is null)
        {
            return;
        }

        Sync(Inputs, [.. inputExecPins, .. inputDataPins, .. inputTypePins]);
        Sync(Outputs, [.. outputExecPins, .. outputDataPins, .. outputTypePins]);
        Sync(PinRows, BuildPinRows());
        OnPropertyChanged(nameof(IsPure));
        OnPropertyChanged(nameof(Role));
    }

    private static void Sync<T>(ObservableCollection<T> target, List<T> desired)
    {
        if (target.SequenceEqual(desired))
        {
            return;
        }

        target.Clear();
        foreach (var item in desired)
        {
            target.Add(item);
        }
    }

    /// <summary>
    /// Builds this node's pin rows (OWN-05b, called after <see cref="Inputs"/> and <see cref="Outputs"/>
    /// are up to date): entry-style pairing for a method/event entry node, return-style pairing for a
    /// return node, index pairing (row i = <c>Inputs[i]</c>, <c>Outputs[i]</c>) for everything else.
    /// </summary>
    private List<PinRowViewModel> BuildPinRows() => Node switch
    {
        MethodEntryNode or EventEntryNode => BuildEntryRows(),
        ReturnNode => BuildReturnRows(),
        _ => BuildIndexedRows(),
    };

    /// <summary>
    /// Rows for a method/event entry node: the Exec pin alone in row 0 (nothing pairs with it), then
    /// one row per argument pairing its <see cref="Node.InputTypePins"/> entry (or nothing, for an
    /// event override argument, which has none) with its <see cref="Node.OutputDataPins"/> entry, then
    /// one row per generic <see cref="Node.OutputTypePins"/> entry (nothing pairs with those either).
    /// </summary>
    private List<PinRowViewModel> BuildEntryRows()
    {
        var rows = new List<PinRowViewModel>();

        if (outputExecPins.Count > 0)
        {
            rows.Add(new PinRowViewModel(null, outputExecPins[0]));
        }

        for (int i = 0; i < outputDataPins.Count; i++)
        {
            rows.Add(new PinRowViewModel(i < inputTypePins.Count ? inputTypePins[i] : null, outputDataPins[i]));
        }

        foreach (var genericPin in outputTypePins)
        {
            rows.Add(new PinRowViewModel(null, genericPin));
        }

        return rows;
    }

    /// <summary>
    /// Rows for a return node: the Exec (return) pin alone in row 0, then one row per return value
    /// pairing its <see cref="Node.InputDataPins"/> entry with its <see cref="Node.InputTypePins"/>
    /// entry. Both pins of a pair are naturally on the node's input side; the type pin is rendered in
    /// the row's right column purely to keep it next to its data pin.
    /// </summary>
    private List<PinRowViewModel> BuildReturnRows()
    {
        var rows = new List<PinRowViewModel>();

        if (inputExecPins.Count > 0)
        {
            rows.Add(new PinRowViewModel(inputExecPins[0], null));
        }

        for (int i = 0; i < inputDataPins.Count; i++)
        {
            rows.Add(new PinRowViewModel(inputDataPins[i], i < inputTypePins.Count ? inputTypePins[i] : null));
        }

        return rows;
    }

    /// <summary>Rows for every other node kind: row i pairs <see cref="Inputs"/>[i] with <see cref="Outputs"/>[i].</summary>
    private List<PinRowViewModel> BuildIndexedRows()
    {
        int count = Math.Max(Inputs.Count, Outputs.Count);
        var rows = new List<PinRowViewModel>(count);

        for (int i = 0; i < count; i++)
        {
            rows.Add(new PinRowViewModel(i < Inputs.Count ? Inputs[i] : null, i < Outputs.Count ? Outputs[i] : null));
        }

        return rows;
    }

    private void OnNodePositionChanged(Node node, double positionX, double positionY) => OnPropertyChanged(nameof(Location));

    private void OnInputTypeChanged(object? sender, EventArgs e) => OnPropertyChanged(nameof(Label));

    private void OnGraphPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MethodGraph.Modifiers))
        {
            foreach (var pin in AllPins)
            {
                pin.RefreshSelfHint();
            }
        }
    }

    private void OnNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(Node.Name):
                OnPropertyChanged(nameof(Name));
                break;
            // Derived node constructors set these after the base constructor added the node to the
            // graph (and created this view model), so dependent values are refreshed here.
            case nameof(MakeArrayNode.UsePredefinedSize):
            case nameof(CallMethodNode.MethodSpecifier):
            case nameof(ConstructorNode.ConstructorSpecifier):
                UpdateOverloads();
                OnPropertyChanged(nameof(CurrentOverload));
                OnPropertyChanged(nameof(Label));
                OnPropertyChanged(nameof(VisualKind));
                OnPropertyChanged(nameof(KindIconId));
                OnPropertyChanged(nameof(Role));
                OnPropertyChanged(nameof(ToolTip));
                OnPropertyChanged(nameof(CanSetPure));
                OnPropertyChanged(nameof(IsPure));
                break;
        }
    }

    /// <summary>
    /// Unsubscribes from the wrapped node's events and disposes every pin view model and pin
    /// collection.
    /// </summary>
    public void Dispose()
    {
        Node.OnPositionChanged -= OnNodePositionChanged;
        Node.InputTypeChanged -= OnInputTypeChanged;
        nodeNotifier.PropertyChanged -= OnNodePropertyChanged;
        if (Node.Graph is INotifyPropertyChanged graphNotifier)
        {
            graphNotifier.PropertyChanged -= OnGraphPropertyChanged;
        }

        foreach (var collection in new IDisposable[] { inputExecPins, inputDataPins, inputTypePins, outputExecPins, outputDataPins, outputTypePins })
        {
            collection.Dispose();
        }

        foreach (var pin in AllPins)
        {
            pin.ConnectionChanged -= OnPinConnectionChanged;
            pin.Dispose();
        }
    }
}
