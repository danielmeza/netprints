#nullable enable
using System.Runtime.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using NetPrints.Core;

namespace NetPrints.Graph
{
    /// <summary>
    /// Abstract base class for node pins.
    /// </summary>
    [DataContract]
    [KnownType(typeof(NodeInputDataPin))]
    [KnownType(typeof(NodeOutputDataPin))]
    [KnownType(typeof(NodeInputExecPin))]
    [KnownType(typeof(NodeOutputExecPin))]
    [KnownType(typeof(NodeInputTypePin))]
    [KnownType(typeof(NodeOutputTypePin))]
    public abstract partial class NodePin : ModelObject
    {
        /// <summary>
        /// Name of the pin.
        /// </summary>
        [ObservableProperty]
        [DataMember]
        public partial string Name { get; set; }

        /// <summary>
        /// Node this pin is contained in.
        /// </summary>
        [DataMember]
        public Node Node
        {
            get;
            private set;
        }

        /// <summary>
        /// Sets <see cref="Node"/> and <see cref="Name"/>. Does not add the pin to
        /// <paramref name="node"/>'s pin collection; callers do that (see <see cref="Node.AddInputDataPin"/>
        /// and the other <c>Add*Pin</c> helpers).
        /// </summary>
        /// <param name="node">Node the pin belongs to.</param>
        /// <param name="name">Name of the pin.</param>
        protected NodePin(Node node, string name)
        {
            Node = node;
            Name = name;
        }

        /// <summary>
        /// Returns <see cref="Name"/>.
        /// </summary>
        /// <returns><see cref="Name"/>.</returns>
        public override string ToString()
        {
            return Name;
        }
    }
}
