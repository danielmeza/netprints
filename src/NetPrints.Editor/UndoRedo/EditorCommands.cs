using System.Runtime.CompilerServices;
using NetPrints.Core;
using NetPrints.Graph;

namespace NetPrints.Editor.UndoRedo;

/// <summary>
/// Undoable editor commands (replaces the WPF <c>NetPrintsCommands</c> routed commands and their
/// <c>MakeUndoCommand</c> table). Undo pairs follow PAR-60.
/// </summary>
public static class EditorCommands
{
    /// <summary>Message of the <see cref="InvalidOperationException"/> thrown when a command's undo runs before its own <see cref="IUndoableCommand.Execute"/>.</summary>
    private const string NoDoActionMessage = "Undo called before the command's Do action.";

    /// <summary>Adds a variable of type <c>object</c>; undo removes it.</summary>
    public static IUndoableCommand AddVariable(ClassGraph cls, string name)
    {
        Variable? variable = null;
        return new DelegateUndoableCommand("Add variable",
            () =>
            {
                variable ??= new Variable(cls, name, TypeSpecifier.FromType<object>(), null, null, VariableModifiers.None);
                cls.Variables.Add(variable);
            },
            () => cls.Variables.Remove(variable ?? throw new InvalidOperationException(NoDoActionMessage)));
    }

    /// <summary>Removes a variable; undo restores the same variable at its position.</summary>
    public static IUndoableCommand RemoveVariable(ClassGraph cls, Variable variable)
    {
        int index = -1;
        return new DelegateUndoableCommand("Remove variable",
            () =>
            {
                index = cls.Variables.IndexOf(variable);
                cls.Variables.Remove(variable);
            },
            () => cls.Variables.Insert(Math.Clamp(index, 0, cls.Variables.Count), variable));
    }

    /// <summary>Adds an event graph (US4); undo removes it (redo restores the same graph).</summary>
    public static IUndoableCommand AddEventGraph(ClassGraph cls, EventGraph graph) =>
        new DelegateUndoableCommand("Add event graph",
            () => cls.EventGraphs.Add(graph),
            () => cls.EventGraphs.Remove(graph));

    /// <summary>Removes an event graph (US4); undo restores it at its position.</summary>
    public static IUndoableCommand RemoveEventGraph(ClassGraph cls, EventGraph graph)
    {
        int index = -1;
        return new DelegateUndoableCommand("Remove event graph",
            () =>
            {
                index = cls.EventGraphs.IndexOf(graph);
                cls.EventGraphs.Remove(graph);
            },
            () => cls.EventGraphs.Insert(Math.Clamp(index, 0, cls.EventGraphs.Count), graph));
    }

    /// <summary>Adds a getter; undo removes it (redo restores the same getter).</summary>
    public static IUndoableCommand AddGetter(Variable variable)
    {
        MethodGraph? getter = null;
        return new DelegateUndoableCommand("Add getter",
            () => variable.GetterMethod = getter ??= ModelOperations.CreateGetter(variable),
            () => variable.GetterMethod = null);
    }

    /// <summary>Removes the getter; undo restores it.</summary>
    public static IUndoableCommand RemoveGetter(Variable variable)
    {
        MethodGraph? old = null;
        return new DelegateUndoableCommand("Remove getter",
            () =>
            {
                old = variable.GetterMethod;
                variable.GetterMethod = null;
            },
            () => variable.GetterMethod = old);
    }

    /// <summary>Adds a setter; undo removes it (redo restores the same setter).</summary>
    public static IUndoableCommand AddSetter(Variable variable)
    {
        MethodGraph? setter = null;
        return new DelegateUndoableCommand("Add setter",
            () => variable.SetterMethod = setter ??= ModelOperations.CreateSetter(variable),
            () => variable.SetterMethod = null);
    }

    /// <summary>Removes the setter; undo restores it.</summary>
    public static IUndoableCommand RemoveSetter(Variable variable)
    {
        MethodGraph? old = null;
        return new DelegateUndoableCommand("Remove setter",
            () =>
            {
                old = variable.SetterMethod;
                variable.SetterMethod = null;
            },
            () => variable.SetterMethod = old);
    }

    /// <summary>
    /// A node that survives overload changes. Changing the overload of a call or constructor node
    /// replaces the node, so every overload command of that node goes through one shared handle
    /// that follows the replacements; otherwise undoing two changes in a row would act on a node
    /// that is no longer in the graph.
    /// </summary>
    private sealed class NodeHandle(Node node)
    {
        public Node Node { get; set; } = node;
    }

    private static readonly ConditionalWeakTable<Node, NodeHandle> NodeHandles = [];

    /// <summary>Changes the overload of a node; undo restores the previous overload (PAR-40, PAR-60).</summary>
    public static IUndoableCommand ChangeOverload(Node node, object newOverload)
    {
        var handle = NodeHandles.GetValue(node, n => new NodeHandle(n));
        object? previous = null;

        void Apply(object overload)
        {
            var replacement = ModelOperations.ChangeOverload(handle.Node, overload);
            NodeHandles.AddOrUpdate(replacement, handle);
            handle.Node = replacement;
        }

        return new DelegateUndoableCommand("Change overload",
            () =>
            {
                previous = ModelOperations.GetCurrentOverload(handle.Node);
                Apply(newOverload);
            },
            () =>
            {
                if (previous is not null)
                {
                    Apply(previous);
                }
            });
    }

    /// <summary>Name of the command <see cref="AddNode"/> returns.</summary>
    public const string AddNodeName = "Add node";

    /// <summary>
    /// Records the creation of a node the caller already made, with the connections made at creation, for
    /// <see cref="UndoRedoStack.Record"/>: undo disconnects and removes it, redo puts it back at its index and reconnects it.
    /// </summary>
    /// <param name="node">The new node, already in its graph.</param>
    /// <returns>The command, named <see cref="AddNodeName"/>.</returns>
    public static IUndoableCommand AddNode(Node node)
    {
        var handle = NodeHandles.GetValue(node, n => new NodeHandle(n));
        NodeConnectionSnapshot? removed = null;

        return new DelegateUndoableCommand(AddNodeName,
            () =>
            {
                if (removed is not null)
                {
                    RestoreAndReconnect(removed);
                }
            },
            () => removed = CaptureAndDisconnect(handle));
    }

    /// <summary>
    /// Removes nodes from their graph; undo puts them back at their indexes and restores every connection, including
    /// those between the removed nodes.
    /// </summary>
    /// <param name="nodes">The nodes to remove, all of one graph.</param>
    /// <returns>The command, named <c>Delete node</c> or <c>Delete nodes</c>.</returns>
    public static IUndoableCommand RemoveNodes(IReadOnlyList<Node> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        var handles = nodes.Select(HandleOf).ToArray();
        var removed = new List<NodeConnectionSnapshot>();

        return new DelegateUndoableCommand(nodes.Count == 1 ? "Delete node" : "Delete nodes",
            () =>
            {
                removed.Clear();
                removed.AddRange(handles.Select(CaptureAndDisconnect));
            },
            () =>
            {
                for (int i = removed.Count - 1; i >= 0; i--)
                {
                    RestoreAndReconnect(removed[i]);
                }
            });
    }

    /// <summary>Adds a method (or an override, when <paramref name="create"/> builds one) to the class; undo removes it and redo restores the same graph.</summary>
    /// <param name="cls">The class.</param>
    /// <param name="create">Creates the method and adds it to the class on the first run; may return null when nothing could be created.</param>
    public static IUndoableCommand AddMethod(ClassGraph cls, Func<MethodGraph?> create)
    {
        MethodGraph? method = null;
        return new DelegateUndoableCommand("Add method",
            () =>
            {
                if (method is null)
                {
                    method = create();
                }
                else
                {
                    cls.Methods.Add(method);
                }
            },
            () =>
            {
                if (method is not null)
                {
                    cls.Methods.Remove(method);
                }
            });
    }

    /// <summary>Adds a constructor to the class; undo removes it and redo restores the same graph.</summary>
    public static IUndoableCommand AddConstructor(ClassGraph cls, ConstructorGraph constructor) =>
        new DelegateUndoableCommand("Add constructor",
            () => cls.Constructors.Add(constructor),
            () => cls.Constructors.Remove(constructor));

    /// <summary>Removes a method or constructor; undo puts it back at its index.</summary>
    public static IUndoableCommand RemoveMethod(ClassGraph cls, ExecutionGraph graph)
    {
        int index = -1;
        return new DelegateUndoableCommand("Remove method",
            () =>
            {
                if (graph is MethodGraph method)
                {
                    index = cls.Methods.IndexOf(method);
                    cls.Methods.Remove(method);
                }
                else if (graph is ConstructorGraph constructor)
                {
                    index = cls.Constructors.IndexOf(constructor);
                    cls.Constructors.Remove(constructor);
                }
            },
            () =>
            {
                if (graph is MethodGraph method)
                {
                    cls.Methods.Insert(Math.Clamp(index, 0, cls.Methods.Count), method);
                }
                else if (graph is ConstructorGraph constructor)
                {
                    cls.Constructors.Insert(Math.Clamp(index, 0, cls.Constructors.Count), constructor);
                }
            });
    }

    /// <summary>
    /// Renames <paramref name="method"/> and every call and delegate node in <paramref name="classes"/> that
    /// refers to it (<see cref="MemberRename.RenameMethod"/>); undo reverses both.
    /// </summary>
    public static IUndoableCommand RenameMethod(IReadOnlyList<ClassGraph> classes, MethodGraph method, string newName)
    {
        RenameResult? result = null;
        return new DelegateUndoableCommand("Rename method",
            () => result = MemberRename.RenameMethod(classes, method, newName),
            () => (result ?? throw new InvalidOperationException(NoDoActionMessage)).Undo());
    }

    /// <summary>
    /// Renames <paramref name="variable"/> and every getter and setter node in <paramref name="classes"/> that
    /// refers to it (<see cref="MemberRename.RenameVariable"/>); undo reverses both.
    /// </summary>
    public static IUndoableCommand RenameVariable(IReadOnlyList<ClassGraph> classes, Variable variable, string newName)
    {
        RenameResult? result = null;
        return new DelegateUndoableCommand("Rename variable",
            () => result = MemberRename.RenameVariable(classes, variable, newName),
            () => (result ?? throw new InvalidOperationException(NoDoActionMessage)).Undo());
    }

    /// <summary>Renames <paramref name="graph"/> (<see cref="EventGraph.Rename"/>); undo restores the name.</summary>
    public static IUndoableCommand RenameEventGraph(EventGraph graph, string newName)
    {
        RenameResult? result = null;
        return new DelegateUndoableCommand("Rename event graph",
            () => result = graph.Rename(newName),
            () => (result ?? throw new InvalidOperationException(NoDoActionMessage)).Undo());
    }

    /// <summary>Adds a local variable of type <c>object</c> named <paramref name="name"/> (US5); undo removes it.</summary>
    public static IUndoableCommand AddLocalVariable(ExecutionGraph graph, string name)
    {
        LocalVariable? local = null;
        return new DelegateUndoableCommand("Add local variable",
            () =>
            {
                local ??= new LocalVariable(name, TypeSpecifier.FromType<object>());
                graph.LocalVariables.Add(local);
            },
            () => graph.LocalVariables.Remove(local ?? throw new InvalidOperationException(NoDoActionMessage)));
    }

    /// <summary>
    /// Renames a local variable (US5) and retargets any of its existing getter/setter nodes in
    /// <paramref name="graph"/> to the new name (<see cref="VariableNode.Retarget(VariableSpecifier)"/>,
    /// which keeps their pins and connections since only the name changes); undo reverses both.
    /// </summary>
    public static IUndoableCommand RenameLocalVariable(ExecutionGraph graph, LocalVariable local, string newName)
    {
        string? oldName = null;
        return new DelegateUndoableCommand("Rename local variable",
            () =>
            {
                oldName = local.Name;
                local.Name = newName;
                RetargetLocalVariableNodes(graph, oldName, local);
            },
            () =>
            {
                string currentName = local.Name;
                local.Name = oldName ?? throw new InvalidOperationException(NoDoActionMessage);
                RetargetLocalVariableNodes(graph, currentName, local);
            });
    }

    /// <summary>
    /// Retypes a local variable (US5) and replaces any of its existing getter/setter nodes in
    /// <paramref name="graph"/> with a node of the new type (a type change alters the node's pin
    /// shape, unlike a rename): position and execution connections are preserved; data connections are
    /// not (the old pin's type no longer matches). Like <see cref="RemoveLocalVariable"/>, the original
    /// nodes are captured with <see cref="CaptureAndDisconnect"/> so undo restores the exact same
    /// instances and every connection, and the replacement nodes are built once and reused, so redo
    /// re-applies the same swap rather than minting new node ids each cycle. Undo reverses both.
    /// </summary>
    public static IUndoableCommand RetypeLocalVariable(ExecutionGraph graph, LocalVariable local, TypeSpecifier newType)
    {
        TypeSpecifier? oldType = null;
        List<LocalVariableNodeSwap>? swaps = null;

        return new DelegateUndoableCommand("Retype local variable",
            () =>
            {
                oldType = local.Type;
                local.Type = newType;

                if (swaps is null)
                {
                    swaps = BuildLocalVariableNodeSwaps(graph, local);
                }
                else
                {
                    // Redo: undo just restored the original nodes via RestoreAndReconnect, so swap the
                    // same replacement instances back in rather than building fresh ones (stable ids).
                    // Disconnect and remove every original first, then reconnect every replacement
                    // through the original->replacement map: otherwise a connection between two
                    // originals (e.g. a chained setter) would reconnect to a not-yet-swapped original.
                    foreach (var swap in swaps)
                    {
                        GraphUtil.DisconnectNodePins(swap.Original.Node);
                        graph.Nodes.Remove(swap.Original.Node);
                    }

                    var replacementByOriginalNode = swaps.ToDictionary(s => s.Original.Node, s => s.Replacement);

                    foreach (var swap in swaps)
                    {
                        graph.Nodes.Add(swap.Replacement);
                        ConnectExecOnly(swap.Original, swap.Replacement, replacementByOriginalNode);
                    }
                }
            },
            () =>
            {
                local.Type = oldType ?? throw new InvalidOperationException(NoDoActionMessage);

                var currentSwaps = swaps ?? throw new InvalidOperationException(NoDoActionMessage);

                // Remove every replacement first, then restore the originals in ascending index
                // order: RestoreAndReconnect inserts each at its captured index, so restoring out of
                // that order would shift later insertions and change the final node order.
                foreach (var swap in currentSwaps)
                {
                    GraphUtil.DisconnectNodePins(swap.Replacement);
                    graph.Nodes.Remove(swap.Replacement);
                }

                foreach (var original in currentSwaps.Select(s => s.Original).OrderBy(o => o.Index))
                {
                    RestoreAndReconnect(original);
                }
            });
    }

    /// <summary>
    /// Removes a local variable (US5) and any of its existing getter/setter nodes in
    /// <paramref name="graph"/>; undo restores the local at its position and the nodes with their
    /// original positions and connections.
    /// </summary>
    public static IUndoableCommand RemoveLocalVariable(ExecutionGraph graph, LocalVariable local)
    {
        int index = -1;
        List<NodeConnectionSnapshot>? removedNodes = null;
        return new DelegateUndoableCommand("Remove local variable",
            () =>
            {
                index = graph.LocalVariables.IndexOf(local);
                graph.LocalVariables.Remove(local);
                removedNodes = FindLocalVariableNodes(graph, local.Name).Select(HandleOf).Select(CaptureAndDisconnect).ToList();
            },
            () =>
            {
                graph.LocalVariables.Insert(Math.Clamp(index, 0, graph.LocalVariables.Count), local);
                foreach (var snapshot in removedNodes ?? throw new InvalidOperationException(NoDoActionMessage))
                {
                    RestoreAndReconnect(snapshot);
                }
            });
    }

    /// <summary>The getter/setter nodes of a local variable named <paramref name="name"/> in <paramref name="graph"/>, materialized (safe to remove nodes from the graph while iterating the result).</summary>
    private static List<VariableNode> FindLocalVariableNodes(ExecutionGraph graph, string name) =>
        graph.Nodes.OfType<VariableNode>().Where(n => n.IsLocalVariable && n.Variable.Name == name).ToList();

    /// <summary>Retargets every getter/setter node currently named <paramref name="oldName"/> to <paramref name="local"/>'s current specifier (a rename, same pin shape).</summary>
    private static void RetargetLocalVariableNodes(ExecutionGraph graph, string oldName, LocalVariable local)
    {
        var specifier = local.ToSpecifier();
        foreach (var node in FindLocalVariableNodes(graph, oldName))
        {
            node.Retarget(specifier);
        }
    }

    /// <summary>An original getter/setter node, captured with <see cref="CaptureAndDisconnect"/>, paired with the replacement node built for it by a retype (see <see cref="RetypeLocalVariable"/>).</summary>
    private sealed class LocalVariableNodeSwap
    {
        public required NodeConnectionSnapshot Original { get; init; }
        public required Node Replacement { get; init; }
    }

    /// <summary>
    /// Captures every getter/setter node of <paramref name="local"/> whose type is now stale, and builds a
    /// fresh replacement node of the current specifier for each, at the same position and with the same
    /// execution connections (not data connections: the old pin's type no longer matches). Every original
    /// is captured before any of them is disconnected, so a connection between two stale nodes (e.g. a
    /// chained `set x` -> `set x`) is recorded against the real original pin on both sides rather than a
    /// half-built replacement; connections between two originals are then remapped onto their
    /// replacements when reconnecting.
    /// </summary>
    private static List<LocalVariableNodeSwap> BuildLocalVariableNodeSwaps(ExecutionGraph graph, LocalVariable local)
    {
        var specifier = local.ToSpecifier();
        var staleNodes = FindLocalVariableNodes(graph, local.Name).Where(n => n.Variable.Type != specifier.Type).ToList();
        var originals = staleNodes.Select(HandleOf).Select(CaptureConnections).ToList();

        foreach (var original in originals)
        {
            GraphUtil.DisconnectNodePins(original.Node);
            original.Node.Graph.Nodes.Remove(original.Node);
        }

        var swaps = new List<LocalVariableNodeSwap>();

        foreach (var original in originals)
        {
            Node replacement = original.Node switch
            {
                VariableGetterNode => new VariableGetterNode(graph, specifier),
                VariableSetterNode => new VariableSetterNode(graph, specifier),
                _ => throw new InvalidOperationException($"Unexpected local variable node kind '{original.Node.GetType().Name}'."),
            };

            replacement.PositionX = original.Node.PositionX;
            replacement.PositionY = original.Node.PositionY;

            swaps.Add(new LocalVariableNodeSwap { Original = original, Replacement = replacement });
        }

        var replacementByOriginalNode = swaps.ToDictionary(s => s.Original.Node, s => s.Replacement);

        foreach (var swap in swaps)
        {
            ConnectExecOnly(swap.Original, swap.Replacement, replacementByOriginalNode);
        }

        return swaps;
    }

    /// <summary>
    /// Reconnects <paramref name="replacement"/>'s execution pins from <paramref name="original"/>'s
    /// captured connections (its data connections are not restored: the old pin's type no longer matches
    /// the replacement's). When a captured endpoint belongs to another original node that is itself being
    /// swapped, it is redirected to that original's replacement instead, through
    /// <paramref name="replacementByOriginalNode"/>.
    /// </summary>
    private static void ConnectExecOnly(NodeConnectionSnapshot original, Node replacement, IReadOnlyDictionary<Node, Node> replacementByOriginalNode)
    {
        for (int i = 0; i < original.InputExecIncoming.Count && i < replacement.InputExecPins.Count; i++)
        {
            foreach (var fromRef in original.InputExecIncoming[i])
            {
                var from = fromRef.Handle.Node.OutputExecPins[fromRef.Index];
                var source = replacementByOriginalNode.TryGetValue(from.Node, out var replacedFrom)
                    ? replacedFrom.OutputExecPins[fromRef.Index]
                    : from;
                GraphUtil.ConnectExecPins(source, replacement.InputExecPins[i]);
            }
        }

        for (int i = 0; i < original.OutputExecOutgoing.Count && i < replacement.OutputExecPins.Count; i++)
        {
            if (original.OutputExecOutgoing[i] is { } toRef)
            {
                var to = toRef.Handle.Node.InputExecPins[toRef.Index];
                var target = replacementByOriginalNode.TryGetValue(to.Node, out var replacedTo)
                    ? replacedTo.InputExecPins[toRef.Index]
                    : to;
                GraphUtil.ConnectExecPins(replacement.OutputExecPins[i], target);
            }
        }
    }

    private static NodeHandle HandleOf(Node node) => NodeHandles.GetValue(node, n => new NodeHandle(n));

    /// <summary>A pin of a neighbouring node as the node's handle and the pin's index, so it resolves to the current node after an overload change.</summary>
    private readonly record struct PinRef(NodeHandle Handle, int Index);

    private static PinRef ExecOut(NodeOutputExecPin pin) => new(HandleOf(pin.Node), pin.Node.OutputExecPins.IndexOf(pin));

    private static PinRef ExecIn(NodeInputExecPin pin) => new(HandleOf(pin.Node), pin.Node.InputExecPins.IndexOf(pin));

    private static PinRef DataOut(NodeOutputDataPin pin) => new(HandleOf(pin.Node), pin.Node.OutputDataPins.IndexOf(pin));

    private static PinRef DataIn(NodeInputDataPin pin) => new(HandleOf(pin.Node), pin.Node.InputDataPins.IndexOf(pin));

    /// <summary>A removed node's position, index and every pin connection, captured so <see cref="RestoreAndReconnect"/> can undo the removal exactly.</summary>
    private sealed class NodeConnectionSnapshot
    {
        public required NodeHandle Handle { get; init; }
        public Node Node => Handle.Node;
        public required int Index { get; init; }
        public required IReadOnlyList<IReadOnlyList<PinRef>> InputExecIncoming { get; init; }
        public required IReadOnlyList<PinRef?> OutputExecOutgoing { get; init; }
        public required IReadOnlyList<PinRef?> InputDataIncoming { get; init; }
        public required IReadOnlyList<IReadOnlyList<PinRef>> OutputDataOutgoing { get; init; }
    }

    /// <summary>Records every connection of the node behind <paramref name="handle"/>, then disconnects and removes it.</summary>
    private static NodeConnectionSnapshot CaptureAndDisconnect(NodeHandle handle)
    {
        var node = handle.Node;
        var snapshot = CaptureConnections(handle);
        GraphUtil.DisconnectNodePins(node);
        node.Graph.Nodes.Remove(node);
        return snapshot;
    }

    /// <summary>Records every connection of the node behind <paramref name="handle"/> without disconnecting or removing it.</summary>
    private static NodeConnectionSnapshot CaptureConnections(NodeHandle handle)
    {
        var node = handle.Node;
        return new()
        {
            Handle = handle,
            Index = node.Graph.Nodes.IndexOf(node),
            InputExecIncoming = node.InputExecPins.Select(p => (IReadOnlyList<PinRef>)p.IncomingPins.Select(ExecOut).ToArray()).ToArray(),
            OutputExecOutgoing = node.OutputExecPins.Select(p => p.OutgoingPin is { } to ? ExecIn(to) : (PinRef?)null).ToArray(),
            InputDataIncoming = node.InputDataPins.Select(p => p.IncomingPin is { } from ? DataOut(from) : (PinRef?)null).ToArray(),
            OutputDataOutgoing = node.OutputDataPins.Select(p => (IReadOnlyList<PinRef>)p.OutgoingPins.Select(DataIn).ToArray()).ToArray(),
        };
    }

    /// <summary>Reinserts a captured node at its original position and restores every recorded connection.</summary>
    private static void RestoreAndReconnect(NodeConnectionSnapshot snapshot)
    {
        var node = snapshot.Handle.Node;
        node.Graph.Nodes.Insert(Math.Clamp(snapshot.Index, 0, node.Graph.Nodes.Count), node);

        for (int i = 0; i < snapshot.InputExecIncoming.Count; i++)
        {
            foreach (var from in snapshot.InputExecIncoming[i])
            {
                GraphUtil.ConnectExecPins(from.Handle.Node.OutputExecPins[from.Index], node.InputExecPins[i]);
            }
        }

        for (int i = 0; i < snapshot.OutputExecOutgoing.Count; i++)
        {
            if (snapshot.OutputExecOutgoing[i] is { } to)
            {
                GraphUtil.ConnectExecPins(node.OutputExecPins[i], to.Handle.Node.InputExecPins[to.Index]);
            }
        }

        for (int i = 0; i < snapshot.InputDataIncoming.Count; i++)
        {
            if (snapshot.InputDataIncoming[i] is { } from)
            {
                GraphUtil.ConnectDataPins(from.Handle.Node.OutputDataPins[from.Index], node.InputDataPins[i]);
            }
        }

        for (int i = 0; i < snapshot.OutputDataOutgoing.Count; i++)
        {
            foreach (var to in snapshot.OutputDataOutgoing[i])
            {
                GraphUtil.ConnectDataPins(node.OutputDataPins[i], to.Handle.Node.InputDataPins[to.Index]);
            }
        }
    }
}
