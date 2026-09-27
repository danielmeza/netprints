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

    /// <summary>Removes a method or constructor. As in the WPF editor, undo does nothing.</summary>
    public static IUndoableCommand RemoveMethod(ClassGraph cls, ExecutionGraph graph)
    {
        return new DelegateUndoableCommand("Remove method",
            () =>
            {
                if (graph is MethodGraph method)
                {
                    cls.Methods.Remove(method);
                }
                else if (graph is ConstructorGraph constructor)
                {
                    cls.Constructors.Remove(constructor);
                }
            },
            () => { });
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
    /// <paramref name="graph"/> with a fresh node of the new type (a type change alters the node's pin
    /// shape, unlike a rename): position and execution connections are preserved, matching
    /// <see cref="ModelOperations.ChangeOverload(Node, object)"/>'s existing behavior for overload
    /// changes; data connections are not (the old pin's type no longer matches). Undo reverses both.
    /// </summary>
    public static IUndoableCommand RetypeLocalVariable(ExecutionGraph graph, LocalVariable local, TypeSpecifier newType)
    {
        TypeSpecifier? oldType = null;
        return new DelegateUndoableCommand("Retype local variable",
            () =>
            {
                oldType = local.Type;
                local.Type = newType;
                ReplaceLocalVariableNodes(graph, local);
            },
            () =>
            {
                local.Type = oldType ?? throw new InvalidOperationException(NoDoActionMessage);
                ReplaceLocalVariableNodes(graph, local);
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
                removedNodes = FindLocalVariableNodes(graph, local.Name).Select(CaptureAndDisconnect).ToList();
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

    /// <summary>Replaces every getter/setter node of <paramref name="local"/> whose type is now stale with a fresh one built from its current specifier (a retype, different pin shape).</summary>
    private static void ReplaceLocalVariableNodes(ExecutionGraph graph, LocalVariable local)
    {
        var specifier = local.ToSpecifier();
        foreach (var node in FindLocalVariableNodes(graph, local.Name).Where(n => n.Variable.Type != specifier.Type))
        {
            Node replacement = node switch
            {
                VariableGetterNode => new VariableGetterNode(graph, specifier),
                VariableSetterNode => new VariableSetterNode(graph, specifier),
                _ => throw new InvalidOperationException($"Unexpected local variable node kind '{node.GetType().Name}'."),
            };

            replacement.PositionX = node.PositionX;
            replacement.PositionY = node.PositionY;

            NodeOutputExecPin[] incoming = node.InputExecPins.Count > 0 ? node.InputExecPins[0].IncomingPins.ToArray() : [];
            NodeInputExecPin? outgoing = node.OutputExecPins.Count > 0 ? node.OutputExecPins[0].OutgoingPin : null;

            GraphUtil.DisconnectNodePins(node);
            graph.Nodes.Remove(node); // replacement already added itself to graph.Nodes on construction (Node's base constructor)

            foreach (var from in incoming)
            {
                GraphUtil.ConnectExecPins(from, replacement.InputExecPins[0]);
            }

            if (outgoing is not null)
            {
                GraphUtil.ConnectExecPins(replacement.OutputExecPins[0], outgoing);
            }
        }
    }

    /// <summary>A removed node's position, index and every pin connection, captured so <see cref="RestoreAndReconnect"/> can undo the removal exactly.</summary>
    private sealed class NodeConnectionSnapshot
    {
        public required Node Node { get; init; }
        public required int Index { get; init; }
        public required IReadOnlyList<IReadOnlyList<NodeOutputExecPin>> InputExecIncoming { get; init; }
        public required IReadOnlyList<NodeInputExecPin?> OutputExecOutgoing { get; init; }
        public required IReadOnlyList<NodeOutputDataPin?> InputDataIncoming { get; init; }
        public required IReadOnlyList<IReadOnlyList<NodeInputDataPin>> OutputDataOutgoing { get; init; }
    }

    /// <summary>Records every connection of <paramref name="node"/>, then disconnects and removes it.</summary>
    private static NodeConnectionSnapshot CaptureAndDisconnect(Node node)
    {
        var snapshot = new NodeConnectionSnapshot
        {
            Node = node,
            Index = node.Graph.Nodes.IndexOf(node),
            InputExecIncoming = node.InputExecPins.Select(p => (IReadOnlyList<NodeOutputExecPin>)p.IncomingPins.ToArray()).ToArray(),
            OutputExecOutgoing = node.OutputExecPins.Select(p => p.OutgoingPin).ToArray(),
            InputDataIncoming = node.InputDataPins.Select(p => p.IncomingPin).ToArray(),
            OutputDataOutgoing = node.OutputDataPins.Select(p => (IReadOnlyList<NodeInputDataPin>)p.OutgoingPins.ToArray()).ToArray(),
        };

        GraphUtil.DisconnectNodePins(node);
        node.Graph.Nodes.Remove(node);
        return snapshot;
    }

    /// <summary>Reinserts a captured node at its original position and restores every recorded connection.</summary>
    private static void RestoreAndReconnect(NodeConnectionSnapshot snapshot)
    {
        var node = snapshot.Node;
        node.Graph.Nodes.Insert(Math.Clamp(snapshot.Index, 0, node.Graph.Nodes.Count), node);

        for (int i = 0; i < snapshot.InputExecIncoming.Count; i++)
        {
            foreach (var from in snapshot.InputExecIncoming[i])
            {
                GraphUtil.ConnectExecPins(from, node.InputExecPins[i]);
            }
        }

        for (int i = 0; i < snapshot.OutputExecOutgoing.Count; i++)
        {
            if (snapshot.OutputExecOutgoing[i] is { } to)
            {
                GraphUtil.ConnectExecPins(node.OutputExecPins[i], to);
            }
        }

        for (int i = 0; i < snapshot.InputDataIncoming.Count; i++)
        {
            if (snapshot.InputDataIncoming[i] is { } from)
            {
                GraphUtil.ConnectDataPins(from, node.InputDataPins[i]);
            }
        }

        for (int i = 0; i < snapshot.OutputDataOutgoing.Count; i++)
        {
            foreach (var to in snapshot.OutputDataOutgoing[i])
            {
                GraphUtil.ConnectDataPins(node.OutputDataPins[i], to);
            }
        }
    }
}
