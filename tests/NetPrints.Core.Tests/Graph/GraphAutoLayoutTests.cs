using NetPrints.Core;
using NetPrints.Graph;
using Xunit;

namespace NetPrints.Tests.Graph
{
    /// <summary>DF-T20, model part: <see cref="GraphAutoLayout.PlaceUnpositioned"/>.</summary>
    public class GraphAutoLayoutTests
    {
        [Fact]
        public void UpstreamNeighbourPlacesTheNodeToItsRightAtTheSameHeight()
        {
            var method = new MethodGraph("M");
            method.EntryNode.PositionX = 100;
            method.EntryNode.PositionY = 50;
            method.MainReturnNode.PositionX = -99999;
            method.MainReturnNode.PositionY = 999999;

            var target = new IfElseNode(method);
            GraphUtil.ConnectExecPins(method.EntryNode.InitialExecutionPin, target.ExecutionPin);

            GraphAutoLayout.PlaceUnpositioned(method, new Node[] { target });

            Assert.Equal(400, target.PositionX);
            Assert.Equal(50, target.PositionY);
        }

        [Fact]
        public void DownstreamOnlyNeighbourPlacesTheNodeToItsLeftAtTheSameHeight()
        {
            var method = new MethodGraph("M");
            method.EntryNode.PositionX = -99999;
            method.EntryNode.PositionY = 999999;
            method.MainReturnNode.PositionX = -99999;
            method.MainReturnNode.PositionY = 999999;

            var downstream = new IfElseNode(method) { PositionX = 700, PositionY = 50 };
            var target = new IfElseNode(method);
            GraphUtil.ConnectExecPins(target.TruePin, downstream.ExecutionPin);

            GraphAutoLayout.PlaceUnpositioned(method, new Node[] { target });

            Assert.Equal(400, target.PositionX);
            Assert.Equal(50, target.PositionY);
        }

        [Fact]
        public void NoNeighbourFallsBackPastTheRightmostPlacedNode()
        {
            var graph = new TypeGraph();
            graph.ReturnNode.PositionX = -99999;
            graph.ReturnNode.PositionY = 999999;

            var placedA = new TypeNode(graph, TypeSpecifier.FromType<int>()) { PositionX = 0, PositionY = 20 };
            var placedB = new TypeNode(graph, TypeSpecifier.FromType<int>()) { PositionX = 600, PositionY = 80 };
            var target = new TypeNode(graph, TypeSpecifier.FromType<int>());

            GraphAutoLayout.PlaceUnpositioned(graph, new Node[] { target });

            Assert.Equal(900, target.PositionX);
            Assert.Equal(20, target.PositionY);
        }

        [Fact]
        public void ASecondFallbackNodeStepsDownByRowSpacing()
        {
            var graph = new TypeGraph();
            graph.ReturnNode.PositionX = -99999;
            graph.ReturnNode.PositionY = 999999;

            var placedA = new TypeNode(graph, TypeSpecifier.FromType<int>()) { PositionX = 0, PositionY = 20 };
            var placedB = new TypeNode(graph, TypeSpecifier.FromType<int>()) { PositionX = 600, PositionY = 80 };
            var first = new TypeNode(graph, TypeSpecifier.FromType<int>());
            var second = new TypeNode(graph, TypeSpecifier.FromType<int>());

            GraphAutoLayout.PlaceUnpositioned(graph, new Node[] { first, second });

            Assert.Equal(900, first.PositionX);
            Assert.Equal(20, first.PositionY);
            Assert.Equal(900, second.PositionX);
            Assert.Equal(140, second.PositionY);
        }

        [Fact]
        public void ACollisionMovesTheCandidateDownByRowSpacing()
        {
            var method = new MethodGraph("M");
            method.EntryNode.PositionX = 100;
            method.EntryNode.PositionY = 50;
            method.MainReturnNode.PositionX = -99999;
            method.MainReturnNode.PositionY = 999999;

            var blocker = new IfElseNode(method) { PositionX = 400, PositionY = 50 };
            var target = new IfElseNode(method);
            GraphUtil.ConnectExecPins(method.EntryNode.InitialExecutionPin, target.ExecutionPin);

            GraphAutoLayout.PlaceUnpositioned(method, new Node[] { target });

            Assert.Equal(400, target.PositionX);
            Assert.Equal(170, target.PositionY);
        }

        [Fact]
        public void AnEmptyGraphPlacesFallbackNodesAtTheOrigin()
        {
            var graph = new TypeGraph();
            var second = new TypeNode(graph, TypeSpecifier.FromType<int>());

            GraphAutoLayout.PlaceUnpositioned(graph, new Node[] { graph.ReturnNode, second });

            Assert.Equal(0, graph.ReturnNode.PositionX);
            Assert.Equal(0, graph.ReturnNode.PositionY);
            Assert.Equal(0, second.PositionX);
            Assert.Equal(120, second.PositionY);
        }

        [Fact]
        public void PlacementIsDeterministicForTheSameInput()
        {
            static (double X, double Y) PlaceOnce()
            {
                var graph = new TypeGraph();
                graph.ReturnNode.PositionX = -99999;
                graph.ReturnNode.PositionY = 999999;
                var placedA = new TypeNode(graph, TypeSpecifier.FromType<int>()) { PositionX = 0, PositionY = 20 };
                var placedB = new TypeNode(graph, TypeSpecifier.FromType<int>()) { PositionX = 600, PositionY = 80 };
                var target = new TypeNode(graph, TypeSpecifier.FromType<int>());

                GraphAutoLayout.PlaceUnpositioned(graph, new Node[] { target });

                return (target.PositionX, target.PositionY);
            }

            Assert.Equal(PlaceOnce(), PlaceOnce());
        }
    }
}
