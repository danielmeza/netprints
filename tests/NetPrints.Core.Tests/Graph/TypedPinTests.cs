using System;
using System.Linq;
using System.Reflection;
using NetPrints.Core;
using NetPrints.Graph;
using Xunit;

namespace NetPrints.Tests.Graph
{
    /// <summary>The typed data pins and the pin attributes of the declarative pin convention (FR-009, FR-010).</summary>
    public class TypedPinTests
    {
        private static ForLoopNode Node() => new(new MethodGraph("M"));

        [Fact]
        public void TypedInputPinDefaultsToDefaultOfT()
        {
            NodeInputDataPin<int> number = new(Node(), "number");
            NodeInputDataPin<bool> flag = new(Node(), "flag");
            NodeInputDataPin<string> text = new(Node(), "text");

            Assert.Equal(0, number.UnconnectedValue);
            Assert.Equal(false, flag.UnconnectedValue);
            Assert.Null(text.UnconnectedValue);
        }

        [Fact]
        public void TypedPinsTakeTheirTypeFromT()
        {
            NodeInputDataPin<int> input = new(Node(), "in");
            NodeOutputDataPin<string> output = new(Node(), "out");

            Assert.Equal(TypeSpecifier.FromType<int>(), input.PinType.Value);
            Assert.Equal(TypeSpecifier.FromType<string>(), output.PinType.Value);
        }

        [Fact]
        public void TypedPinsNeedNoGenericMath()
        {
            Assert.Empty(typeof(NodeInputDataPin<>).GetGenericArguments().Single().GetGenericParameterConstraints());
            Assert.Empty(typeof(NodeOutputDataPin<>).GetGenericArguments().Single().GetGenericParameterConstraints());
        }

        [Fact]
        public void TypedPinsAreDataPins()
        {
            ForLoopNode node = Node();
            NodeInputDataPin<int> input = new(node, "in");
            NodeOutputDataPin<int> output = new(node, "out");

            Assert.IsAssignableFrom<NodeInputDataPin>(input);
            Assert.IsAssignableFrom<NodeOutputDataPin>(output);
        }

        [Fact]
        public void UntypedPinsBehaveAsBefore()
        {
            NodeInputDataPin untyped = new(Node(), "in", new ObservableValue<BaseType>(TypeSpecifier.FromType<int>()));

            Assert.Null(untyped.UnconnectedValue);
        }

        [Fact]
        public void PinKeyDefaultsToNullAndRoleToArgument()
        {
            PinAttribute plain = new();
            PinAttribute keyed = new("InitialIndex");

            Assert.Null(plain.Key);
            Assert.Equal("InitialIndex", keyed.Key);
            Assert.Equal(PinRole.Argument, keyed.Role);
        }

        [Theory]
        [InlineData(typeof(NodeKindAttribute), AttributeTargets.Class, false)]
        [InlineData(typeof(NodeBaseAttribute), AttributeTargets.Class, false)]
        [InlineData(typeof(PinAttribute), AttributeTargets.Property, false)]
        [InlineData(typeof(PinTypeAttribute), AttributeTargets.Property, false)]
        [InlineData(typeof(NodePinLayoutAttribute), AttributeTargets.Class, false)]
        public void AttributesTargetTheirMembers(Type attribute, AttributeTargets targets, bool inherited)
        {
            AttributeUsageAttribute usage = attribute.GetCustomAttribute<AttributeUsageAttribute>()
                ?? throw new InvalidOperationException(attribute.Name);

            Assert.Equal(targets, usage.ValidOn);
            Assert.Equal(inherited, usage.Inherited);
        }

        [Fact]
        public void NodeKindCarriesItsId()
        {
            Assert.Equal("netprints.loop", new NodeKindAttribute("netprints.loop").Id);
            Assert.Equal("InitialIndex", new PinTypeAttribute("InitialIndex").Member);
        }

        [Fact]
        public void LayoutKeepsTheDeclaredPinsInOrder()
        {
            NodePinLayoutAttribute layout = new("in.exec.Execution", "out.data.Index");

            Assert.Equal(["in.exec.Execution", "out.data.Index"], layout.Pins);
        }
    }
}
