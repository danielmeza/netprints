using NetPrints.Core;
using NetPrints.Graph;
using Xunit;

namespace NetPrints.Tests.Graph
{
    /// <summary>The pins agree with each other by the time <see cref="NodeOutputExecPin.OutgoingPinChanged"/> is raised.</summary>
    public class ExecPinConnectionOrderTests
    {
        private static (NodeOutputExecPin From, NodeInputExecPin To) Pins()
        {
            var graph = new MethodGraph("M");
            var from = new IfElseNode(graph);
            var to = new IfElseNode(graph);
            return (from.TruePin, to.ExecPin);
        }

        [Fact]
        public void ConnectingExposesTheIncomingPinWhenTheOutgoingPinChanges()
        {
            var (from, to) = Pins();
            bool seen = false;
            from.OutgoingPinChanged += (pin, _, _) => seen = to.IncomingPins.Contains(pin);

            GraphUtil.ConnectExecPins(from, to);

            Assert.True(seen);
        }

        [Fact]
        public void DisconnectingAnInputPinClearsItsIncomingPinsBeforeTheOutgoingPinChanges()
        {
            var (from, to) = Pins();
            GraphUtil.ConnectExecPins(from, to);
            int remaining = -1;
            from.OutgoingPinChanged += (_, _, _) => remaining = to.IncomingPins.Count;

            GraphUtil.DisconnectInputExecPin(to);

            Assert.Equal(0, remaining);
        }
    }
}
