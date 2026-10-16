using NetPrints.Core;
using NetPrints.Graph;

namespace NetPrints.Editor.UndoRedo;

/// <summary>
/// Model operations shared by view models and undoable commands (ported from the WPF view models).
/// </summary>
public static class ModelOperations
{
    /// <summary>Text of the <see cref="MakeArrayNode"/> overload that switches to initializer-list mode.</summary>
    public const string UseInitializerList = MakeArrayNode.UseInitializerListOverload;

    /// <summary>Text of the <see cref="MakeArrayNode"/> overload that switches to predefined-size mode.</summary>
    public const string UsePredefinedSize = MakeArrayNode.UsePredefinedSizeOverload;

    /// <summary>Creates the getter method of a variable (entry and return nodes connected, typed return).</summary>
    public static MethodGraph CreateGetter(Variable variable)
    {
        var method = new MethodGraph($"get_{variable.Name}")
        {
            Class = variable.Class,
            Visibility = variable.Visibility,
        };

        method.EntryNode.PositionX = GraphUtil.NewMethodEntryPositionX;
        method.EntryNode.PositionY = GraphUtil.NewMethodEntryPositionY;
        method.MainReturnNode.PositionX = method.EntryNode.PositionX + GraphUtil.NewMethodReturnOffsetX;
        method.MainReturnNode.PositionY = method.EntryNode.PositionY;

        GraphUtil.ConnectExecPins(method.EntryNode.ExecPin, method.MainReturnNode.ExecPin);

        const int offsetX = -308;
        const int offsetY = -112;
        TypeNode returnTypeNode = GraphUtil.CreateNestedTypeNode(method, variable.Type,
            method.MainReturnNode.PositionX + offsetX, method.MainReturnNode.PositionY + offsetY);
        method.MainReturnNode.AddReturnType();
        GraphUtil.ConnectTypePins(returnTypeNode.OutputTypePins[0], method.MainReturnNode.InputTypePins[0]);

        return method;
    }

    /// <summary>Creates the setter method of a variable (entry and return nodes connected, typed argument).</summary>
    public static MethodGraph CreateSetter(Variable variable)
    {
        var method = new MethodGraph($"set_{variable.Name}")
        {
            Class = variable.Class,
            Visibility = variable.Visibility,
        };

        method.EntryNode.PositionX = GraphUtil.NewMethodEntryPositionX;
        method.EntryNode.PositionY = GraphUtil.NewMethodEntryPositionY;
        method.MainReturnNode.PositionX = method.EntryNode.PositionX + GraphUtil.NewMethodReturnOffsetX;
        method.MainReturnNode.PositionY = method.EntryNode.PositionY;

        GraphUtil.ConnectExecPins(method.EntryNode.ExecPin, method.MainReturnNode.ExecPin);

        const int offsetX = -308;
        const int offsetY = -112;
        TypeNode argTypeNode = GraphUtil.CreateNestedTypeNode(method, variable.Type,
            method.EntryNode.PositionX + offsetX, method.EntryNode.PositionY + offsetY);
        method.MethodEntryNode.AddArgument();
        GraphUtil.ConnectTypePins(argTypeNode.OutputTypePins[0], method.EntryNode.InputTypePins[0]);

        return method;
    }

    /// <summary>The current overload of a node, or null when the node has none.</summary>
    public static object? GetCurrentOverload(Node node) => (node as IOverloadableNode)?.CurrentOverload;

    /// <summary>
    /// Changes the overload of a node. Call and constructor nodes are replaced by a new node at
    /// the same position with the same purity and reconnected execution pins; make-array nodes
    /// switch their size mode in place. Returns the node that now represents the original one.
    /// </summary>
    public static Node ChangeOverload(Node node, object overload)
    {
        if (node is not IOverloadableNode overloadable)
        {
            throw new InvalidOperationException("Tried to change the overload of a node that does not support overloads.");
        }

        Node? newNode = overloadable.ChangeOverload(overload);
        if (newNode is null)
        {
            return node;
        }

        // Remember the old execution connections to restore them on the new node.
        NodeOutputExecPin[]? oldIncomingPins = null;
        NodeInputExecPin? oldOutgoingPin = null;

        bool oldPurity = node.IsPure;
        if (!oldPurity)
        {
            oldIncomingPins = node.InputExecPins[0].IncomingPins.ToArray();
            oldOutgoingPin = node.OutputExecPins[0].OutgoingPin;
        }

        GraphUtil.DisconnectNodePins(node);
        node.Graph.Nodes.Remove(node);

        newNode.PositionX = node.PositionX;
        newNode.PositionY = node.PositionY;

        if (newNode.CanSetPure)
        {
            newNode.IsPure = oldPurity;
        }

        if (!newNode.IsPure)
        {
            if (oldOutgoingPin != null)
            {
                GraphUtil.ConnectExecPins(newNode.OutputExecPins[0], oldOutgoingPin);
            }

            foreach (var oldIncomingPin in oldIncomingPins ?? [])
            {
                GraphUtil.ConnectExecPins(oldIncomingPin, newNode.InputExecPins[0]);
            }
        }

        return newNode;
    }
}
