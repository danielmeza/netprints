#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
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
    [NodeKind("classReturn")]
    public sealed partial class ClassReturnNode : Node, IPinListEditor
    {
        /// <summary>
        /// Input type pin for the class's base type.
        /// </summary>
        public partial NodeInputTypePin BaseTypePin { get; }

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
            DeclarePins();
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

        /// <inheritdoc />
        bool IPinListEditor.ShowsPinButtons => true;

        /// <inheritdoc />
        string IPinListEditor.AddPinLabel => "Add interface";

        /// <inheritdoc />
        string IPinListEditor.RemovePinLabel => "Remove interface";

        /// <inheritdoc />
        bool IPinListEditor.CanRemovePin => InterfacePins.Any();

        /// <inheritdoc />
        bool IPinListEditor.ChangesMethodSignature => false;

        /// <inheritdoc />
        IReadOnlyList<int>? IPinListEditor.SlotsAfterAdd => null;

        /// <inheritdoc />
        IReadOnlyList<int>? IPinListEditor.SlotsAfterRemove => null;

        /// <inheritdoc />
        void IPinListEditor.AddPin() => AddInterfacePin();

        /// <inheritdoc />
        void IPinListEditor.RemovePin() => RemoveInterfacePin();
    }
}
