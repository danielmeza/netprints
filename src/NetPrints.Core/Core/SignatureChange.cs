#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using NetPrints.Graph;

namespace NetPrints.Core;

/// <summary>
/// Moves the call and delegate nodes of a method or custom event onto a new parameter list when the member's arguments
/// change, so the generated callers keep matching the generated signature.
/// </summary>
public static class SignatureChange
{
    /// <summary>
    /// Rebuilds every call and make-delegate node that refers to <paramref name="oldKey"/> on <paramref name="newParameters"/>.
    /// A rebuilt node keeps its position, purity and execution connections; the target and each argument keep their connection
    /// and unconnected value through <paramref name="sourceIndexes"/>, and a removed argument's connection is dropped.
    /// </summary>
    /// <param name="classes">The classes whose graphs may use the member (the whole project).</param>
    /// <param name="oldKey">The member's identity before the change.</param>
    /// <param name="newParameters">The member's parameters after the change.</param>
    /// <param name="sourceIndexes">For each new parameter, the index it had before, or -1 for a new one.</param>
    /// <returns>The action that puts the original nodes and their connections back.</returns>
    public static Action RetargetCallers(IEnumerable<ClassGraph> classes, MemberKey oldKey, IReadOnlyList<Named<BaseType>> newParameters, IReadOnlyList<int> sourceIndexes)
    {
        ArgumentNullException.ThrowIfNull(classes);
        ArgumentNullException.ThrowIfNull(newParameters);
        ArgumentNullException.ThrowIfNull(sourceIndexes);

        List<Node> callers = [.. classes.SelectMany(GraphKeys.AllGraphs).SelectMany(graph => graph.Nodes)
            .Where(node => node is CallMethodNode or MakeDelegateNode && ((IMemberReferencingNode)node).RefersTo(oldKey))];
        List<Action> undos = [];

        foreach (Node node in callers)
        {
            undos.Add(node switch
            {
                CallMethodNode call => Replace(call, new CallMethodNode(call.Graph, WithParameters(call.MethodSpecifier, newParameters)), newParameters, sourceIndexes),
                MakeDelegateNode makeDelegate => Replace(makeDelegate, new MakeDelegateNode(makeDelegate.Graph, WithParameters(makeDelegate.MethodSpecifier, newParameters)), newParameters, sourceIndexes),
                _ => () => { }
                ,
            });
        }

        return () =>
        {
            for (int i = undos.Count - 1; i >= 0; i--)
            {
                undos[i]();
            }
        };
    }

    private static MethodSpecifier WithParameters(MethodSpecifier specifier, IReadOnlyList<Named<BaseType>> parameters) =>
        new(specifier.Name,
            parameters.Select(parameter => new MethodParameter(parameter.Name, parameter.Value, MethodParameterPassType.Default, false, null)),
            specifier.ReturnTypes, specifier.Modifiers, specifier.Visibility, specifier.DeclaringType, specifier.GenericArguments);

    private static Action Replace(Node old, Node replacement, IReadOnlyList<Named<BaseType>> newParameters, IReadOnlyList<int> sourceIndexes)
    {
        NodeGraph graph = old.Graph;
        int index = graph.Nodes.IndexOf(old);
        List<Action> restore = Capture(old);

        replacement.PositionX = old.PositionX;
        replacement.PositionY = old.PositionY;
        if (replacement.CanSetPure)
        {
            replacement.IsPure = old.IsPure;
        }

        Carry(old, replacement, newParameters, sourceIndexes);
        GraphUtil.DisconnectNodePins(old);
        graph.Nodes.Remove(old);

        return () =>
        {
            GraphUtil.DisconnectNodePins(replacement);
            graph.Nodes.Remove(replacement);
            graph.Nodes.Insert(Math.Min(index, graph.Nodes.Count), old);
            restore.ForEach(action => action());
        };
    }

    private static void Carry(Node old, Node replacement, IReadOnlyList<Named<BaseType>> newParameters, IReadOnlyList<int> sourceIndexes)
    {
        for (int i = 0; i < Math.Min(old.InputExecPins.Count, replacement.InputExecPins.Count); i++)
        {
            foreach (NodeOutputExecPin incoming in old.InputExecPins[i].IncomingPins.ToList())
            {
                GraphUtil.ConnectExecPins(incoming, replacement.InputExecPins[i]);
            }
        }

        foreach (NodeOutputExecPin oldPin in old.OutputExecPins)
        {
            NodeOutputExecPin? newPin = replacement.OutputExecPins.FirstOrDefault(pin => pin.Name == oldPin.Name);
            if (newPin is not null && oldPin.OutgoingPin is { } outgoing)
            {
                GraphUtil.ConnectExecPins(newPin, outgoing);
            }
        }

        int oldOffset = old is CallMethodNode { IsStatic: true } ? 0 : 1;
        int newOffset = replacement is CallMethodNode { IsStatic: true } ? 0 : 1;
        if (old is MakeDelegateNode oldDelegate)
        {
            oldOffset = oldDelegate.IsFromStaticMethod ? 0 : 1;
        }

        if (replacement is MakeDelegateNode newDelegate)
        {
            newOffset = newDelegate.IsFromStaticMethod ? 0 : 1;
        }

        if (oldOffset == 1 && newOffset == 1 && old.InputDataPins[0].IncomingPin is { } targetSource)
        {
            GraphUtil.ConnectDataPins(targetSource, replacement.InputDataPins[0]);
        }

        if (old is CallMethodNode)
        {
            for (int i = 0; i < newParameters.Count && i < sourceIndexes.Count; i++)
            {
                int source = sourceIndexes[i];
                NodeInputDataPin to = replacement.InputDataPins[newOffset + i];
                NodeInputDataPin? from = source >= 0 && oldOffset + source < old.InputDataPins.Count ? old.InputDataPins[oldOffset + source] : null;
                if (from?.IncomingPin is { } incoming)
                {
                    GraphUtil.ConnectDataPins(incoming, to);
                }
                else if (from?.UnconnectedValue is { } value && Equals(from.PinType.Value, to.PinType.Value))
                {
                    to.UnconnectedValue = value;
                }
                else if (DefaultLiteral(to) is { } fallback)
                {
                    to.UnconnectedValue = fallback;
                }
            }
        }

        foreach (NodeOutputDataPin oldPin in old.OutputDataPins)
        {
            NodeOutputDataPin? newPin = replacement.OutputDataPins.FirstOrDefault(pin => pin.Name == oldPin.Name && Equals(pin.PinType.Value, oldPin.PinType.Value));
            if (newPin is null)
            {
                continue;
            }

            foreach (NodeInputDataPin target in oldPin.OutgoingPins.ToList())
            {
                GraphUtil.ConnectDataPins(newPin, target);
            }
        }
    }

    private static object? DefaultLiteral(NodeInputDataPin pin)
    {
        if (!pin.UsesUnconnectedValue || pin.PinType.Value is not TypeSpecifier { IsEnum: false } type)
        {
            return null;
        }

        if (type == TypeSpecifier.FromType<string>())
        {
            return string.Empty;
        }

        return PrimitiveDefaults.TryGetValue(type.Name, out object? value) ? value : null;
    }

    private static readonly Dictionary<string, object> PrimitiveDefaults = new()
    {
        [typeof(bool).FullName ?? "System.Boolean"] = false,
        [typeof(byte).FullName ?? "System.Byte"] = (byte)0,
        [typeof(sbyte).FullName ?? "System.SByte"] = (sbyte)0,
        [typeof(short).FullName ?? "System.Int16"] = (short)0,
        [typeof(ushort).FullName ?? "System.UInt16"] = (ushort)0,
        [typeof(int).FullName ?? "System.Int32"] = 0,
        [typeof(uint).FullName ?? "System.UInt32"] = 0u,
        [typeof(long).FullName ?? "System.Int64"] = 0L,
        [typeof(ulong).FullName ?? "System.UInt64"] = 0UL,
        [typeof(float).FullName ?? "System.Single"] = 0f,
        [typeof(double).FullName ?? "System.Double"] = 0d,
        [typeof(decimal).FullName ?? "System.Decimal"] = 0m,
        [typeof(char).FullName ?? "System.Char"] = '\0',
    };

    private static List<Action> Capture(Node node)
    {
        List<Action> restore = [];
        foreach (NodeInputDataPin pin in node.InputDataPins)
        {
            if (pin.IncomingPin is { } from)
            {
                restore.Add(() => GraphUtil.ConnectDataPins(from, pin));
            }
        }

        foreach (NodeOutputDataPin pin in node.OutputDataPins)
        {
            foreach (NodeInputDataPin to in pin.OutgoingPins.ToList())
            {
                restore.Add(() => GraphUtil.ConnectDataPins(pin, to));
            }
        }

        foreach (NodeInputExecPin pin in node.InputExecPins)
        {
            foreach (NodeOutputExecPin from in pin.IncomingPins.ToList())
            {
                restore.Add(() => GraphUtil.ConnectExecPins(from, pin));
            }
        }

        foreach (NodeOutputExecPin pin in node.OutputExecPins)
        {
            if (pin.OutgoingPin is { } to)
            {
                restore.Add(() => GraphUtil.ConnectExecPins(pin, to));
            }
        }

        foreach (NodeInputTypePin pin in node.InputTypePins)
        {
            if (pin.IncomingPin is { } from)
            {
                restore.Add(() => GraphUtil.ConnectTypePins(from, pin));
            }
        }

        foreach (NodeOutputTypePin pin in node.OutputTypePins)
        {
            foreach (NodeInputTypePin to in pin.OutgoingPins.ToList())
            {
                restore.Add(() => GraphUtil.ConnectTypePins(pin, to));
            }
        }

        return restore;
    }
}
