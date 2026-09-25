#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using NetPrints.Core;

namespace NetPrints.Graph
{
    /// <summary>
    /// A <see cref="ClassGraph"/>'s single fixed node (see <see cref="ClassGraph.ReturnNode"/>),
    /// analogous to a method's entry/return nodes: it holds the class's base type and implemented
    /// interfaces as input type pins, so inheritance participates in the same connection and
    /// type-inference machinery as any other node.
    /// </summary>
    [DataContract]
    public class ClassReturnNode : Node
    {
        /// <summary>
        /// Input type pin for the class's base type.
        /// </summary>
        public NodeInputTypePin SuperTypePin
        {
            get => InputTypePins[0];
        }

        /// <summary>
        /// Input type pins for the class's implemented interfaces, one per interface added with
        /// <see cref="AddInterfacePin"/>.
        /// </summary>
        public IEnumerable<NodeInputTypePin> InterfacePins
        {
            get => InputTypePins.Skip(1);
        }

        /// <summary>
        /// Adds this node to <paramref name="graph"/> and gives it its base-type input type pin.
        /// </summary>
        /// <param name="graph">Class graph the node belongs to.</param>
        public ClassReturnNode(ClassGraph graph)
            : base(graph)
        {
            AddInputTypePin("BaseType");
        }

        /// <summary>
        /// Adds one more input type pin to <see cref="InterfacePins"/> for an additional implemented
        /// interface.
        /// </summary>
        public void AddInterfacePin()
        {
            AddInputTypePin($"Interface{InputTypePins.Count}");
        }

        /// <summary>
        /// Removes the last pin added by <see cref="AddInterfacePin"/>, disconnecting it first. Does
        /// nothing if there are no interface pins.
        /// </summary>
        public void RemoveInterfacePin()
        {
            var interfacePin = InterfacePins.LastOrDefault();

            if (interfacePin != null)
            {
                GraphUtil.DisconnectInputTypePin(interfacePin);
                InputTypePins.Remove(interfacePin);
            }
        }
    }
}
