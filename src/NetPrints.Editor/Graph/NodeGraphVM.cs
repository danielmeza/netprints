using System.Collections.ObjectModel;
using System.Collections.Specialized;
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
using NetPrints.Editor.UndoRedo;
using NetPrints.Editor.Variables;
using NetPrints.Graph;

namespace NetPrints.Editor.Graph;

/// <summary>
/// The graph shown in the canvas: nodes, connections, selection, node creation, search and the
/// Get/Set chooser (PAR-37..57).
/// </summary>
public sealed partial class NodeGraphVM : ObservableObject, IDisposable
{
    private readonly HashSet<NodeVM> subscribedNodes = [];
    private readonly Dictionary<(NodePin Source, NodePin Target), ConnectionVM> connectionsByPins = [];

    /// <summary>
    /// Wraps <paramref name="graph"/>: builds its node view models, subscribes to reflection reload,
    /// creates its search and Get/Set chooser view models, and builds the initial connections.
    /// </summary>
    /// <param name="graph">Graph to wrap.</param>
    /// <param name="owner">Class editor view model that owns this graph.</param>
    public NodeGraphVM(NodeGraph graph, ClassEditorVM owner)
    {
        Graph = graph;
        Owner = owner;

        Nodes = new ObservableViewModelCollection<NodeVM, Node>(graph.Nodes, n => new NodeVM(n, this), n => n.Dispose());
        Nodes.CollectionChanged += OnNodesChanged;
        SyncNodeSubscriptions();

        Context.Reflection.Reloaded += OnReflectionReloaded;

        Search = new SuggestionListVM(this);
        GetSetChooser = new GetSetChooserVM(this);

        RebuildConnections();
    }

    /// <summary>The wrapped model graph.</summary>
    public NodeGraph Graph { get; }

    /// <summary>The class editor view model that owns this graph.</summary>
    public ClassEditorVM Owner { get; }

    /// <summary>Host services shared across the editor.</summary>
    public EditorContext Context => Owner.Context;

    /// <summary>View models for <see cref="Graph"/>'s nodes.</summary>
    public ObservableViewModelCollection<NodeVM, Node> Nodes { get; }

    /// <summary>Cables derived from the model's pin connections.</summary>
    public ObservableCollection<ConnectionVM> Connections { get; } = [];

    /// <summary>The currently selected nodes.</summary>
    public IEnumerable<NodeVM> SelectedNodes => Nodes.Where(n => n.IsSelected);

    /// <summary>Node search popup (PAR-52..54).</summary>
    public SuggestionListVM Search { get; }

    /// <summary>Get/Set chooser for variables (PAR-55, 57).</summary>
    public GetSetChooserVM GetSetChooser { get; }

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
            subscribedNodes.Remove(removed);
        }

        foreach (var added in current.Where(n => !subscribedNodes.Contains(n)))
        {
            added.PinsChanged += OnNodePinsChanged;
            subscribedNodes.Add(added);
        }
    }

    private void OnNodePinsChanged(object? sender, EventArgs e) => RebuildConnections();

    /// <summary>
    /// Synchronizes <see cref="Connections"/> with the model. Existing cables keep their state
    /// (for example <see cref="ConnectionVM.IsFaint"/>).
    /// </summary>
    internal void RebuildConnections()
    {
        var pinMap = new Dictionary<NodePin, NodePinVM>();
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
            }

            var connection = new ConnectionVM(source, target);
            connectionsByPins[key] = connection;
            Connections.Add(connection);
        }

        foreach (var pin in pinMap.Values)
        {
            pin.RefreshConnectionState();
        }
    }

    /// <summary>Connects two pins when compatible (PAR-46). Returns whether they were connected.</summary>
    public bool Connect(NodePinVM a, NodePinVM b) => a.ConnectTo(b);

    /// <summary>Toggles the faint state of the cables of a pin (PAR-48).</summary>
    internal void ToggleFaint(NodePinVM pin)
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
    public void SelectNodes(IEnumerable<NodeVM> nodes, bool deselectPrevious)
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

    /// <summary>
    /// Deletes the selected nodes except method entry, class return and the main return node (PAR-37).
    /// </summary>
    public void DeleteSelectedNodes()
    {
        var mainReturn = (Graph as MethodGraph)?.MainReturnNode;

        foreach (var node in SelectedNodes.ToList())
        {
            if (node.Node is MethodEntryNode or ClassReturnNode or ExecutionEntryNode or TypeReturnNode || node.Node == mainReturn)
            {
                continue;
            }

            GraphUtil.DisconnectNodePins(node.Node);
            Graph.Nodes.Remove(node.Node);
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

        object[] parameters = [Graph, .. request.ConstructorParameters];
        var node = (Node)Activator.CreateInstance(request.NodeType, parameters)!;
        node.PositionX = Math.Max(0, request.Position.X);
        node.PositionY = Math.Max(0, request.Position.Y);

        if (request.SuggestionPin is not null)
        {
            var provider = Context.Reflection.Provider;
            GraphUtil.ConnectRelevantPins(request.SuggestionPin, node, provider.TypeSpecifierIsSubclassOf, provider.HasImplicitCast);
        }

        return node;
    }

    /// <summary>Creates a node of type <typeparamref name="T"/> at a position.</summary>
    public Node AddNode<T>(GraphPoint position, NodePin? suggestionPin = null, params object[] arguments) where T : Node =>
        AddNode(new AddNodeRequest(typeof(T), Graph, position, suggestionPin, arguments));

    /// <summary>
    /// Opens the node search at a position. With a pin, the suggestions are filtered for it and the
    /// chosen node is connected to it (PAR-47, 52).
    /// </summary>
    public Task OpenSearchAsync(GraphPoint position, NodePin? suggestionPin = null, CancellationToken cancellationToken = default) =>
        Search.OpenAsync(position, suggestionPin, cancellationToken);

    /// <summary>Changes the overload of a node through the undo stack (PAR-40).</summary>
    public void ChangeOverload(NodeVM node, object overload) => Owner.UndoRedo.Do(EditorCommands.ChangeOverload(node.Node, overload));

    /// <summary>A method or constructor was dropped from the class lists (PAR-56).</summary>
    public Node Drop(MethodVM method, GraphPoint position)
    {
        var declaringClass = Graph.Class ?? throw new InvalidOperationException("The open graph has no class.");
        var declaringType = declaringClass.Type;
        return method.IsConstructor
            ? AddNode<ConstructorNode>(position, null, method.ToConstructorSpecifier(declaringType))
            : AddNode<CallMethodNode>(position, null, method.ToMethodSpecifier(declaringType), new List<BaseType>());
    }

    /// <summary>A variable was dropped from the class list: opens the Get/Set chooser (PAR-57).</summary>
    public void Drop(MemberVariableVM variable, GraphPoint position) => GetSetChooser.Open(variable.Specifier, position);

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
        }

        subscribedNodes.Clear();
        Search.Dispose();

        foreach (var node in Nodes)
        {
            node.Dispose();
        }

        Nodes.Dispose();
    }
}
