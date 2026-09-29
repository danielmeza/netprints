using System.Linq;
using NetPrints.Core;
using NetPrints.Graph;
using Xunit;

namespace NetPrints.Tests.Graph
{
    /// <summary>Regression test for the <see cref="LiteralNode.UpdatePinTypes"/> reference-equality bug
    /// (see implementation-notes.md, "T035 - LiteralNode.UpdatePinTypes...").</summary>
    public class LiteralNodeTests
    {
        [Fact]
        public void ConnectedLiteralKeepsItsConnectionAfterRelax()
        {
            var method = new MethodGraph("M");
            var writer = new MethodSpecifier("Write",
                [new MethodParameter("value", TypeSpecifier.FromType<int>(), MethodParameterPassType.Default, false, null)],
                [], MethodModifiers.Static, MemberVisibility.Public, new TypeSpecifier("C"), []);

            var call = new CallMethodNode(method, writer);
            var literal = LiteralNode.WithValue(method, 42);

            NodeInputDataPin argumentPin = call.InputDataPins.Single(p => p.Name == "value");
            GraphUtil.ConnectDataPins(literal.ValuePin, argumentPin);

            GraphTypeInference.Relax(method);

            Assert.Same(literal.ValuePin, argumentPin.IncomingPin);
        }
    }
}
