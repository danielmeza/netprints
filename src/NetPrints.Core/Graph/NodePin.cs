#nullable enable
using CommunityToolkit.Mvvm.ComponentModel;
using NetPrints.Core;

namespace NetPrints.Graph
{
    /// <summary>
    /// Abstract base class for node pins.
    /// </summary>
    public abstract partial class NodePin : ModelObject
    {
        /// <summary>
        /// Name of the pin.
        /// </summary>
        [ObservableProperty]
        public partial string Name { get; set; }

        /// <summary>
        /// What the pin means to its node; <see cref="PinRole.Argument"/> unless the generator or the node says otherwise.
        /// </summary>
        public PinRole Role { get; set; } = PinRole.Argument;

        /// <summary>
        /// Node this pin is contained in.
        /// </summary>
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
