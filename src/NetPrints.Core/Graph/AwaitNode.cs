#nullable enable
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using NetPrints.Core;

namespace NetPrints.Graph
{
    /// <summary>
    /// Node for awaiting tasks.
    /// </summary>
    [DataContract]
    public class AwaitNode : ExecNode
    {
        /// <summary>
        /// Always <see langword="true"/>: awaiting is not itself considered a side effect worth
        /// sequencing (the connected exec pins already order the surrounding statements).
        /// </summary>
        public override bool CanSetPure => true;

        /// <summary>
        /// Input data pin for the <see cref="Task"/> (or <see cref="Task{TResult}"/>) to await.
        /// </summary>
        public NodeInputDataPin TaskPin => InputDataPins[0];

        /// <summary>
        /// Output data pin for the awaited result, or <see langword="null"/> if the connected task has
        /// no result (a non-generic <see cref="Task"/>).
        /// </summary>
        public NodeOutputDataPin? ResultPin => OutputDataPins.FirstOrDefault();

        /// <summary>
        /// Adds this node to <paramref name="graph"/> and gives it its task pin, adding a result pin
        /// if the (initially unconnected) task type has one.
        /// </summary>
        /// <param name="graph">Graph the node belongs to.</param>
        public AwaitNode(NodeGraph graph)
            : base(graph)
        {
            AddInputDataPin("Task", TypeSpecifier.FromType<Task>());
            SetupEvents();
            UpdateResultPin();
        }

        /// <summary>
        /// Re-subscribes <see cref="UpdateResultPin"/> to <see cref="TaskPin"/>'s
        /// <see cref="NodeInputDataPin.IncomingPinChanged"/> event (event subscriptions are not
        /// serialized).
        /// </summary>
        public override void OnMethodDeserialized()
        {
            base.OnMethodDeserialized();
            SetupEvents();
        }

        /// <summary>
        /// Sets up the task connection changed event which updates
        /// the result type.
        /// </summary>
        private void SetupEvents()
        {
            TaskPin.IncomingPinChanged += (pin, oldPin, newPin) => UpdateResultPin();
        }

        /// <summary>
        /// Updates the result pin's type depending on the incoming task's return type.
        /// </summary>
        private void UpdateResultPin()
        {
            // Check if the task returns a value and add or remove the result
            // pin depending on that.

            TypeSpecifier taskType = (TypeSpecifier)(TaskPin.IncomingPin?.PinType?.Value ?? TypeSpecifier.FromType<Task>());

            if (taskType.GenericArguments.Count > 0)
            {
                BaseType returnType = taskType.GenericArguments[0];

                if (ResultPin != null)
                {
                    ResultPin.PinType.Value = returnType;

                    // Disconnect all existing connections.
                    // Might want them to stay connected but that requires reflection
                    // to determine if the types are still compatible.
                    foreach (var outgoingPin in ResultPin.OutgoingPins)
                    {
                        GraphUtil.DisconnectOutputDataPin(ResultPin);
                    }
                }
                else
                {
                    AddOutputDataPin("Result", returnType);
                }
            }
            else
            {
                // Remove existing result pin if any
                if (ResultPin != null)
                {
                    GraphUtil.DisconnectOutputDataPin(ResultPin);
                    OutputDataPins.Remove(ResultPin);
                }
            }
        }
    }
}
