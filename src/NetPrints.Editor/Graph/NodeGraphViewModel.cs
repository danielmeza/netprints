using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetPrints.Core;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.Graph.GetSet;
using NetPrints.Editor.Graph.Nodes;
using NetPrints.Editor.Graph.Pins;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.ModelSync;
using NetPrints.Editor.Search;
using NetPrints.Editor.Shell;
using NetPrints.Editor.UndoRedo;
using NetPrints.Editor.Variables;
using NetPrints.Extensibility.Nodes;
using NetPrints.Graph;

namespace NetPrints.Editor.Graph;

/// <summary>
/// The graph shown in the canvas: nodes, connections, selection, node creation, search and the
/// Get/Set chooser (PAR-37..57).
/// </summary>
public sealed partial class NodeGraphViewModel : ObservableObject, IDisposable
{
    private readonly HashSet<NodeViewModel> subscribedNodes = [];
    private readonly Dictionary<(NodePin Source, NodePin Target), ConnectionViewModel> connectionsByPins = [];

    /// <summary>
    /// Wraps <paramref name="graph"/>: builds its node view models, subscribes to reflection reload,
    /// creates its search and Get/Set chooser view models, and builds the initial connections.
    /// </summary>
    /// <param name="graph">Graph to wrap.</param>
    /// <param name="services">Narrow services shared with the owning class editor (FR-038).</param>
    public NodeGraphViewModel(NodeGraph graph, ClassEditorServices services)
    {
        Graph = graph;
        Services = services;

        Nodes = new ObservableViewModelCollection<NodeViewModel, Node>(graph.Nodes, n => new NodeViewModel(n, this), n => n.Dispose());
        Nodes.CollectionChanged += OnNodesChanged;
        SyncNodeSubscriptions();

        Context.Reflection.Reloaded += OnReflectionReloaded;

        Search = new SuggestionListViewModel(this);
        GetSetChooser = new GetSetChooserViewModel(this);

        RebuildConnections();
    }

    /// <summary>The wrapped model graph.</summary>
    public NodeGraph Graph { get; }

    /// <summary>Narrow services shared with the owning class editor (FR-038).</summary>
    public ClassEditorServices Services { get; }

    /// <summary>Host services shared across the editor.</summary>
    public EditorContext Context => Services.Context;

    /// <summary>View models for <see cref="Graph"/>'s nodes.</summary>
    public ObservableViewModelCollection<NodeViewModel, Node> Nodes { get; }

    /// <summary>Gets or sets the invoker the canvas's context menus and gestures run commands through, or null (set by the document that shows this graph).</summary>
    public CommandInvoker? Commands { get; set; }

    /// <summary>Cables derived from the model's pin connections.</summary>
    public ObservableCollection<ConnectionViewModel> Connections { get; } = [];

    /// <summary>The currently selected nodes.</summary>
    public IEnumerable<NodeViewModel> SelectedNodes => Nodes.Where(n => n.IsSelected);

    /// <summary>Raised when a node is selected or deselected, or the node set changes.</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>Node search popup (PAR-52..54).</summary>
    public SuggestionListViewModel Search { get; }

    /// <summary>Get/Set chooser for variables (PAR-55, 57).</summary>
    public GetSetChooserViewModel GetSetChooser { get; }

    /// <summary>Graph name, shown as a watermark (PAR-38).</summary>
    public string Name => Graph switch
    {
        MethodGraph method => method.Name,
        ClassGraph cls => cls.Name,
        _ => Graph.ToString() ?? "",
    };

    /// <summary>Whether the wrapped graph is a <see cref="ConstructorGraph"/>.</summary>
    public bool IsConstructor => Graph is ConstructorGraph;

    /// <summary>Grid cell size in graph units, for bindings that need it without a <see cref="GraphConstants"/> reference.</summary>
    public double GridCellSize => GraphConstants.GridCellSize;

    // Connections

    private void OnNodesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SyncNodeSubscriptions();
        RebuildConnections();
    }

    private void SyncNodeSubscriptions()
    {
        var current = Nodes.ToHashSet();

        foreach (var removed in subscribedNodes.Where(n => !current.Contains(n)).ToList())
        {
            removed.PinsChanged -= OnNodePinsChanged;
            removed.PropertyChanged -= OnNodePropertyChanged;
            subscribedNodes.Remove(removed);
        }

        foreach (var added in current.Where(n => !subscribedNodes.Contains(n)))
        {
            added.PinsChanged += OnNodePinsChanged;
            added.PropertyChanged += OnNodePropertyChanged;
            subscribedNodes.Add(added);
        }

        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NodeViewModel.IsSelected))
        {
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnNodePinsChanged(object? sender, EventArgs e) => RebuildConnections();

    /// <summary>
    /// Synchronizes <see cref="Connections"/> with the model. Existing cables keep their state
    /// (for example <see cref="ConnectionViewModel.IsFaint"/>).
    /// </summary>
    internal void RebuildConnections()
    {
        var pinMap = new Dictionary<NodePin, NodePinViewModel>();
        foreach (var node in Nodes)
        {
            foreach (var pin in node.AllPins)
            {
                pinMap[pin.Pin] = pin;
            }
        }

        var desired = new List<(NodePin Source, NodePin Target)>();
        foreach (var pin in pinMap.Keys)
        {
            switch (pin)
            {
                case NodeInputDataPin { IncomingPin: not null } idp:
                    desired.Add((idp.IncomingPin, idp));
                    break;
                case NodeOutputExecPin { OutgoingPin: not null } oxp:
                    desired.Add((oxp, oxp.OutgoingPin));
                    break;
                case NodeInputTypePin { IncomingPin: not null } itp:
                    desired.Add((itp.IncomingPin, itp));
                    break;
            }
        }

        var desiredSet = desired.Where(d => pinMap.ContainsKey(d.Source) && pinMap.ContainsKey(d.Target)).ToHashSet();

        foreach (var key in connectionsByPins.Keys.Where(k => !desiredSet.Contains(k)).ToList())
        {
            Connections.Remove(connectionsByPins[key]);
            connectionsByPins[key].Dispose();
            connectionsByPins.Remove(key);
        }

        foreach (var key in desiredSet)
        {
            var source = pinMap[key.Source];
            var target = pinMap[key.Target];

            if (connectionsByPins.TryGetValue(key, out var existing) && existing.Source == source && existing.Target == target)
            {
                continue;
            }

            if (existing is not null)
            {
                Connections.Remove(existing);
                existing.Dispose();
            }

            var connection = new ConnectionViewModel(source, target);
            connectionsByPins[key] = connection;
            Connections.Add(connection);
        }

        foreach (var pin in pinMap.Values)
        {
            pin.RefreshConnectionState();
        }
    }

    /// <summary>Connects two pins when compatible (PAR-46). Returns whether they were connected.</summary>
    public bool Connect(NodePinViewModel a, NodePinViewModel b) => a.ConnectTo(b);

    /// <summary>Pending-connection release point in graph coordinates, pushed by the view (ED-T09).</summary>
    [ObservableProperty]
    public partial GraphPoint PendingConnectionAnchor { get; set; }

    /// <summary>
    /// A Nodify pending connection completed (PAR-46, PAR-47, ED-T09): connects to a compatible pin,
    /// or opens the node search at <see cref="PendingConnectionAnchor"/> filtered for the source pin
    /// when released on empty canvas.
    /// </summary>
    /// <param name="pins">The source and target connectors' data contexts, as a 2-tuple (Nodify's own
    /// tuple is <c>(object, object)</c>; matched loosely as <see cref="ITuple"/> so a plain
    /// <c>(NodePinViewModel?, NodePinViewModel?)</c> works too, for VM-level tests).</param>
    [RelayCommand]
    private void ConnectionCompleted(object? pins)
    {
        const int ExpectedLength = 2;
        if (pins is not ITuple { Length: ExpectedLength } tuple || tuple[0] is not NodePinViewModel source)
        {
            return;
        }

        if (tuple[1] is NodePinViewModel target)
        {
            Connect(source, target);
        }
        else
        {
            // Not a command, and OpenSearchAsync has no catch of its own: route a fault to the error dialog too.
            OpenSearchAsync(PendingConnectionAnchor, source.Pin).Forget(Context, "Failed to open the node search");
        }
    }

    /// <summary>Toggles the faint state of the cables of a pin (PAR-48).</summary>
    internal void ToggleFaint(NodePinViewModel pin)
    {
        foreach (var connection in Connections.Where(c => c.Source == pin || c.Target == pin))
        {
            connection.ToggleFaint();
        }
    }

    // Selection (PAR-49)

    /// <summary>
    /// Selects <paramref name="nodes"/>, optionally deselecting every other node first.
    /// </summary>
    /// <param name="nodes">Nodes to select.</param>
    /// <param name="deselectPrevious">Whether to deselect every node not in <paramref name="nodes"/> first.</param>
    public void SelectNodes(IEnumerable<NodeViewModel> nodes, bool deselectPrevious)
    {
        var toSelect = nodes.ToHashSet();

        if (deselectPrevious)
        {
            foreach (var node in Nodes.Where(n => !toSelect.Contains(n)))
            {
                node.IsSelected = false;
            }
        }

        foreach (var node in toSelect)
        {
            node.IsSelected = true;
        }
    }


    /// <summary>Deselects every node.</summary>
    [RelayCommand]
    public void DeselectNodes() => SelectNodes([], deselectPrevious: true);

    /// <summary>Selects every node.</summary>
    public void SelectAll() => SelectNodes(Nodes, deselectPrevious: false);

    /// <summary>Gets whether the node search or the Get/Set chooser is open.</summary>
    public bool HasOpenPopup => Search.IsOpen || GetSetChooser.IsOpen;

    /// <summary>Closes the node search and the Get/Set chooser.</summary>
    public void ClosePopups()
    {
        Search.IsOpen = false;
        GetSetChooser.Close();
    }

    /// <summary>Raised by <see cref="RequestView"/>; the editor's behavior carries the request out (ADR-0004: the view owns the viewport).</summary>
    public event EventHandler<GraphViewRequest>? ViewRequested;

    /// <summary>Asks the view to carry out a viewport or popup action.</summary>
    /// <param name="request">The action.</param>
    public void RequestView(GraphViewRequest request) => ViewRequested?.Invoke(this, request);

    /// <summary>Raised after <see cref="RevealNode"/> selects a node, so the view can scroll it into
    /// view (ADR-0004: the view owns the canvas viewport, this view model only asks for it).</summary>
    public event EventHandler<NodeViewModel>? NodeRevealRequested;

    private NodeViewModel? pendingReveal;

    /// <summary>
    /// Selects the node with <paramref name="nodeId"/> and asks the view to bring it into view
    /// (FR-034, ED-T03).
    /// </summary>
    /// <param name="nodeId">Id of the node to reveal.</param>
    /// <returns>Whether a node with that id exists in this graph.</returns>
    public bool RevealNode(string nodeId)
    {
        NodeViewModel? node = Nodes.FirstOrDefault(n => n.Node.Id == nodeId);
        if (node is null)
        {
            return false;
        }

        SelectNodes([node], deselectPrevious: true);
        if (NodeRevealRequested is { } handler)
        {
            handler(this, node);
        }
        else
        {
            pendingReveal = node;
        }

        return true;
    }

    /// <summary>Takes the node <see cref="RevealNode"/> selected while no view was attached to scroll it into view.</summary>
    /// <returns>The node, or <see langword="null"/> when no reveal is waiting.</returns>
    public NodeViewModel? TakePendingReveal()
    {
        NodeViewModel? node = pendingReveal;
        pendingReveal = null;
        return node;
    }

    /// <summary>
    /// Deletes the selected nodes except method entry, class return and the main return node (PAR-37), as one
    /// undoable step that restores the connections.
    /// </summary>
    public void DeleteSelectedNodes()
    {
        var mainReturn = (Graph as MethodGraph)?.MainReturnNode;
        List<Node> deletable =
        [
            .. SelectedNodes.Select(vm => vm.Node)
                .Where(node => node is not (MethodEntryNode or ClassReturnNode or ExecutionEntryNode or TypeReturnNode) && node != mainReturn),
        ];

        if (deletable.Count > 0)
        {
            Services.UndoRedo.Do(EditorCommands.RemoveNodes(deletable));
        }

        DeselectNodes();
    }

    // Node creation (PAR-47, 52..57)

    /// <summary>
    /// Creates a node. Negative positions are clamped to the canvas; when the request has a
    /// suggestion pin, the new node is connected to it (PAR-47).
    /// </summary>
    public Node AddNode(AddNodeRequest request)
    {
        if (request.Graph != Graph)
        {
            throw new ArgumentException("The request targets another graph.", nameof(request));
        }

        return RecordAdd(() => CreateNode(request));
    }

    private Node CreateNode(AddNodeRequest request)
    {
        object[] parameters = [Graph, .. request.ConstructorParameters];
        var node = (Node)(Activator.CreateInstance(request.NodeType, parameters)
            ?? throw new InvalidOperationException($"Could not create an instance of {request.NodeType}."));
        node.PositionX = Math.Max(0, request.Position.X);
        node.PositionY = Math.Max(0, request.Position.Y);

        if (request.SuggestionPin is not null)
        {
            var provider = Context.Reflection.Provider;
            GraphUtil.ConnectRelevantPins(request.SuggestionPin, node, provider.TypeSpecifierIsSubclassOf, provider.HasImplicitCast);
        }

        return node;
    }

    /// <summary>
    /// Creates the node an extension's <see cref="NodeSuggestion"/> offers, positioned and connected like
    /// <see cref="AddNode(AddNodeRequest)"/> (a node constructor adds the node to its graph).
    /// </summary>
    /// <param name="position">Where the node is created (graph coordinates).</param>
    /// <param name="suggestionPin">Pin the search was opened for, or <see langword="null"/>.</param>
    /// <param name="suggestion">The chosen suggestion.</param>
    /// <returns>The new node.</returns>
    public Node AddNode(GraphPoint position, NodePin? suggestionPin, NodeSuggestion suggestion) => RecordAdd(() =>
    {
        Node node = suggestion.Create(Graph);
        node.PositionX = Math.Max(0, position.X);
        node.PositionY = Math.Max(0, position.Y);

        if (suggestionPin is not null)
        {
            var provider = Context.Reflection.Provider;
            GraphUtil.ConnectRelevantPins(suggestionPin, node, provider.TypeSpecifierIsSubclassOf, provider.HasImplicitCast);
        }

        return node;
    });

    private T RecordAdd<T>(Func<T> create)
        where T : Node
    {
        T node = Services.UndoRedo.RunApplying(create);
        Services.UndoRedo.Record(EditorCommands.AddNode(node));
        return node;
    }

    /// <summary>
    /// Creates an event graph entry (US4) directly, bypassing the reflection-based
    /// <see cref="AddNode{T}(GraphPoint, NodePin?, object[])"/> pipeline: <see cref="EventEntryNode"/>'s
    /// constructors take an <see cref="EventGraph"/>, not a <see cref="NodeGraph"/>, and
    /// <see cref="AddNodeRequest"/> validates a constructor by an exact parameter-type match (every
    /// other node type's constructor is declared with a <see cref="NodeGraph"/> first parameter for
    /// exactly this reason), so it can never resolve one of <see cref="EventEntryNode"/>'s.
    /// </summary>
    /// <param name="position">Where the entry is created (graph coordinates).</param>
    /// <param name="create">Constructs the entry from this graph, cast to <see cref="EventGraph"/>.</param>
    /// <returns>The new entry.</returns>
    public EventEntryNode AddEventEntry(GraphPoint position, Func<EventGraph, EventEntryNode> create) => RecordAdd(() =>
    {
        EventEntryNode node = create((EventGraph)Graph);
        node.PositionX = Math.Max(0, position.X);
        node.PositionY = Math.Max(0, position.Y);
        return node;
    });

    /// <summary>Creates a node of type <typeparamref name="T"/> at a position.</summary>
    public Node AddNode<T>(GraphPoint position, NodePin? suggestionPin = null, params object[] arguments) where T : Node =>
        AddNode(new AddNodeRequest(typeof(T), Graph, position, suggestionPin, arguments));

    /// <summary>
    /// Opens the node search at a position. With a pin, the suggestions are filtered for it and the
    /// chosen node is connected to it (PAR-47, 52).
    /// </summary>
    public Task OpenSearchAsync(GraphPoint position, NodePin? suggestionPin = null, CancellationToken cancellationToken = default) =>
        Search.OpenAsync(position, suggestionPin, cancellationToken);

    /// <summary>
    /// Opens the node search at <paramref name="position"/> with no pin (R2-15, ADR-0004): the
    /// keyboard entry point (Ctrl+Space), so the view only computes the fallback position and the
    /// generated command's own reentrancy guard stops a second Ctrl+Space from starting a second
    /// search while one is already opening.
    /// </summary>
    [RelayCommand]
    private Task OpenSearchAsync(GraphPoint position) => OpenSearchAsync(position, suggestionPin: null);

    /// <summary>Changes the overload of a node through the undo stack (PAR-40).</summary>
    public void ChangeOverload(NodeViewModel node, object overload) => Services.UndoRedo.Do(EditorCommands.ChangeOverload(node.Node, overload));

    /// <summary>A method or constructor was dropped from the class lists (PAR-56).</summary>
    public Node Drop(MethodViewModel method, GraphPoint position)
    {
        var declaringClass = method.Graph.Class ?? Graph.Class ?? throw new InvalidOperationException("The open graph has no class.");
        var declaringType = declaringClass.Type;
        return method.IsConstructor
            ? AddNode<ConstructorNode>(position, null, method.ToConstructorSpecifier(declaringType))
            : AddNode<CallMethodNode>(position, null, method.ToMethodSpecifier(declaringType));
    }

    /// <summary>
    /// A variable was dropped from the class list: opens the Get/Set chooser (PAR-57). The chooser's
    /// on-screen position is a view concern (ADR-0004): <c>CanvasPopup</c> anchors it at the pointer.
    /// </summary>
    /// <param name="variable">The dropped variable.</param>
    /// <param name="position">Where the node is created, in graph coordinates.</param>
    public void Drop(MemberVariableViewModel variable, GraphPoint position) => GetSetChooser.Open(variable.Specifier, position);

    /// <summary>A variable was dropped from the project tree: opens the Get/Set chooser the same way (FR-017, PAR-57).</summary>
    /// <param name="variable">The dropped variable.</param>
    /// <param name="position">Where the node is created, in graph coordinates.</param>
    public void Drop(Variable variable, GraphPoint position) => GetSetChooser.Open(variable.Specifier, position);

    /// <summary>
    /// A local variable was dropped from the Variables panel's Method group (US5, sub-phase H): opens
    /// the Get/Set chooser the same way a member variable does. Locals are always readable and
    /// writable from their own method, so the chooser offers both (<see cref="GetSet.GetSetChooserViewModel.Open"/>).
    /// </summary>
    /// <param name="variable">The dropped local variable.</param>
    /// <param name="position">Where the node is created, in graph coordinates.</param>
    public void Drop(LocalVariableViewModel variable, GraphPoint position) => GetSetChooser.Open(variable.Specifier, position);

    private void OnReflectionReloaded(object? sender, EventArgs e)
    {
        foreach (var node in Nodes)
        {
            node.OnReflectionReloaded();
        }
    }

    /// <summary>
    /// Unsubscribes from reflection reload and node events, disposes the search view model, and
    /// disposes every node view model and the node collection.
    /// </summary>
    public void Dispose()
    {
        Context.Reflection.Reloaded -= OnReflectionReloaded;
        Nodes.CollectionChanged -= OnNodesChanged;
        foreach (var node in subscribedNodes)
        {
            node.PinsChanged -= OnNodePinsChanged;
            node.PropertyChanged -= OnNodePropertyChanged;
        }

        subscribedNodes.Clear();
        foreach (var connection in connectionsByPins.Values)
        {
            connection.Dispose();
        }

        connectionsByPins.Clear();
        Search.Dispose();

        foreach (var node in Nodes)
        {
            node.Dispose();
        }

        Nodes.Dispose();
    }
}
