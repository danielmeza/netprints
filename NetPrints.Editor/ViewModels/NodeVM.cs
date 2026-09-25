using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Editor.Commands;

namespace NetPrints.Editor.ViewModels;

/// <summary>
/// A node of the open graph (PAR-39..42). Uses <see cref="NodeVisualKind"/> instead of brushes and
/// <see cref="GraphPoint"/> for its location.
/// </summary>
public sealed partial class NodeVM : ObservableObject, IDisposable
{
    private readonly INotifyPropertyChanged nodeNotifier;
    private readonly ObservableViewModelCollection<NodePinVM, NodeInputExecPin> inputExecPins;
    private readonly ObservableViewModelCollection<NodePinVM, NodeInputDataPin> inputDataPins;
    private readonly ObservableViewModelCollection<NodePinVM, NodeInputTypePin> inputTypePins;
    private readonly ObservableViewModelCollection<NodePinVM, NodeOutputExecPin> outputExecPins;
    private readonly ObservableViewModelCollection<NodePinVM, NodeOutputDataPin> outputDataPins;
    private readonly ObservableViewModelCollection<NodePinVM, NodeOutputTypePin> outputTypePins;
    private bool suppressOverloadSelection;

    public NodeVM(Node node, NodeGraphVM graph)
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

        UpdateOverloads();
    }

    /// <summary>Raised when a pin was added/removed or a pin connection changed.</summary>
    public event EventHandler? PinsChanged;

    public Node Node { get; }

    public NodeGraphVM Graph { get; }

    /// <summary>Input pins in display order: exec, data, type (left column).</summary>
    public ObservableCollection<NodePinVM> Inputs { get; } = [];

    /// <summary>Output pins in display order: exec, data, type (right column).</summary>
    public ObservableCollection<NodePinVM> Outputs { get; } = [];

    public IEnumerable<NodePinVM> AllPins => Inputs.Concat(Outputs);

    public IReadOnlyList<NodePinVM> InputExecPins => inputExecPins;
    public IReadOnlyList<NodePinVM> InputDataPins => inputDataPins;
    public IReadOnlyList<NodePinVM> InputTypePins => inputTypePins;
    public IReadOnlyList<NodePinVM> OutputExecPins => outputExecPins;
    public IReadOnlyList<NodePinVM> OutputDataPins => outputDataPins;
    public IReadOnlyList<NodePinVM> OutputTypePins => outputTypePins;

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
    private bool isSelected;

    /// <summary>Selected nodes are drawn above the others.</summary>
    public int ZIndex => IsSelected ? 1 : 0;

    public string Name => Node.Name;

    public string Label => Node.ToString();

    public bool IsRerouteNode => Node is RerouteNode;

    /// <summary>Header color category (PAR-39).</summary>
    public NodeVisualKind VisualKind => Node switch
    {
        ExecutionEntryNode => NodeVisualKind.Entry,
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
        _ => NodeVisualKind.Default,
    };

    /// <summary>Documentation tooltip for method calls (PAR-39).</summary>
    public string? ToolTip
    {
        get
        {
            if (Node is CallMethodNode callMethodNode)
            {
                try
                {
                    return Graph.Context.Reflection.Provider.GetMethodDocumentation(callMethodNode.MethodSpecifier);
                }
                catch (Exception)
                {
                    return null;
                }
            }

            return null;
        }
    }

    // Overloads (PAR-40)

    /// <summary>Other overloads of call/constructor nodes, or the other size mode of make-array nodes.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowOverloads))]
    private IReadOnlyList<object> overloads = [];

    public bool ShowOverloads => Overloads.Count > 0;

    /// <summary>Chooser selection; choosing an overload changes it through the undo stack.</summary>
    [ObservableProperty]
    private object? selectedOverload;

    partial void OnSelectedOverloadChanged(object? value)
    {
        if (value is null || suppressOverloadSelection)
        {
            return;
        }

        Graph.ChangeOverload(this, value);

        suppressOverloadSelection = true;
        try
        {
            SelectedOverload = null;
        }
        finally
        {
            suppressOverloadSelection = false;
        }
    }

    public object? CurrentOverload => ModelOperations.GetCurrentOverload(Node);

    internal void UpdateOverloads()
    {
        try
        {
            var provider = Graph.Context.Reflection.Provider;
            Overloads = Node switch
            {
                CallMethodNode { MethodSpecifier: not null } call =>
                    provider.GetPublicMethodOverloads(call.MethodSpecifier).Where(m => m != call.MethodSpecifier).Cast<object>().ToList(),
                ConstructorNode { ConstructorSpecifier: not null } ctor =>
                    provider.GetConstructors(ctor.ConstructorSpecifier.DeclaringType)
                        .Where(c => !SameConstructor(c, ctor.ConstructorSpecifier)).Cast<object>().ToList(),
                MakeArrayNode makeArray =>
                    [makeArray.UsePredefinedSize ? ModelOperations.UseInitializerList : ModelOperations.UsePredefinedSize],
                _ => [],
            };
        }
        catch (Exception)
        {
            Overloads = [];
        }
    }

    private static bool SameConstructor(ConstructorSpecifier a, ConstructorSpecifier b) =>
        a.DeclaringType == b.DeclaringType
        && a.Arguments.Select(p => p.Value).SequenceEqual(b.Arguments.Select(p => p.Value));

    // Purity (PAR-41)

    public bool CanSetPure => Node.CanSetPure;

    public bool IsPure
    {
        get => Node.IsPure;
        set
        {
            if (Node.CanSetPure && Node.IsPure != value)
            {
                Node.IsPure = value;
                OnPropertyChanged();
            }
        }
    }

    // +/- pin buttons (PAR-42)

    public bool ShowLeftPinButtons =>
        Node is MakeArrayNode or MethodEntryNode or ClassReturnNode
        || (Node is ReturnNode && Node == Node.MethodGraph?.MainReturnNode);

    public bool ShowRightPinButtons => Node is MethodEntryNode;

    public string LeftPlusToolTip => Node switch
    {
        MakeArrayNode => "Add array element",
        MethodEntryNode => "Add method parameter",
        ReturnNode => "Add method return value",
        ClassReturnNode => "Add interface",
        _ => "",
    };

    public string LeftMinusToolTip => Node switch
    {
        MakeArrayNode => "Remove array element",
        MethodEntryNode => "Remove method parameter",
        ReturnNode => "Remove method return value",
        ClassReturnNode => "Remove interface",
        _ => "",
    };

    public string RightPlusToolTip => Node is MethodEntryNode ? "Add method generic type parameter" : "";

    public string RightMinusToolTip => Node is MethodEntryNode ? "Remove method generic type parameter" : "";

    [RelayCommand]
    private void LeftPinsPlus()
    {
        switch (Node)
        {
            case MakeArrayNode makeArrayNode: makeArrayNode.AddElementPin(); break;
            case MethodEntryNode entryNode: entryNode.AddArgument(); break;
            case ReturnNode returnNode: returnNode.AddReturnType(); break;
            case ClassReturnNode classReturnNode: classReturnNode.AddInterfacePin(); break;
        }
    }

    [RelayCommand]
    private void LeftPinsMinus()
    {
        switch (Node)
        {
            case MakeArrayNode makeArrayNode: makeArrayNode.RemoveElementPin(); break;
            case MethodEntryNode entryNode: entryNode.RemoveArgument(); break;
            case ReturnNode returnNode: returnNode.RemoveReturnType(); break;
            case ClassReturnNode classReturnNode: classReturnNode.RemoveInterfacePin(); break;
        }
    }

    [RelayCommand]
    private void RightPinsPlus()
    {
        if (Node is MethodEntryNode entryNode)
        {
            entryNode.AddGenericArgument();
        }
    }

    [RelayCommand]
    private void RightPinsMinus()
    {
        if (Node is MethodEntryNode entryNode)
        {
            entryNode.RemoveGenericArgument();
        }
    }

    /// <summary>Selects only this node.</summary>
    public void Select() => Graph.SelectNodes([this], deselectPrevious: true);

    private ObservableViewModelCollection<NodePinVM, TPin> CreatePins<TPin>(ObservableRangeCollection<TPin> pins)
        where TPin : NodePin
    {
        var collection = new ObservableViewModelCollection<NodePinVM, TPin>(pins, p =>
        {
            var vm = new NodePinVM(p, this);
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
        OnPropertyChanged(nameof(IsPure));
    }

    private static void Sync(ObservableCollection<NodePinVM> target, List<NodePinVM> desired)
    {
        if (target.SequenceEqual(desired))
        {
            return;
        }

        target.Clear();
        foreach (var pin in desired)
        {
            target.Add(pin);
        }
    }

    private void OnNodePositionChanged(Node node, double positionX, double positionY) => OnPropertyChanged(nameof(Location));

    private void OnInputTypeChanged(object? sender, EventArgs e) => OnPropertyChanged(nameof(Label));

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
                OnPropertyChanged(nameof(ToolTip));
                OnPropertyChanged(nameof(CanSetPure));
                OnPropertyChanged(nameof(IsPure));
                break;
        }
    }

    public void Dispose()
    {
        Node.OnPositionChanged -= OnNodePositionChanged;
        Node.InputTypeChanged -= OnInputTypeChanged;
        nodeNotifier.PropertyChanged -= OnNodePropertyChanged;

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
