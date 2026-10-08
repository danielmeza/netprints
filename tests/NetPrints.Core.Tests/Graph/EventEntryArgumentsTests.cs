using System;
using System.Linq;
using NetPrints.Core;
using NetPrints.Graph;
using Xunit;

namespace NetPrints.Tests.Graph
{
    /// <summary>
    /// US8 (FR-073): a custom event entry's <see cref="EventEntryNode.Arguments"/> (name, type) map to its output
    /// pins in order, with unique, valid C# identifiers.
    /// </summary>
    public class EventEntryArgumentsTests
    {
        private static readonly TypeSpecifier Int = TypeSpecifier.FromType<int>();

        private static readonly TypeSpecifier Text = TypeSpecifier.FromType<string>();

        private static EventEntryNode NewEntry() => new(new EventGraph("Events"), "OnHit");

        [Fact]
        public void AFreshCustomEventHasNoArguments()
        {
            Assert.Empty(NewEntry().Arguments);
        }

        [Fact]
        public void ArgumentsMapToTheOutputPinsInOrder()
        {
            EventEntryNode entry = NewEntry();

            entry.SetArguments([new EventArgument("amount", Int), new EventArgument("source", Text)]);

            Assert.Equal(["amount", "source"], entry.OutputDataPins.Select(pin => pin.Name));
            Assert.Equal<BaseType>([Int, Text], entry.OutputDataPins.Select(pin => pin.PinType.Value ?? TypeSpecifier.FromType<object>()));
            Assert.Equal([new EventArgument("amount", Int), new EventArgument("source", Text)], entry.Arguments);
        }

        [Fact]
        public void ReplacingTheArgumentsKeepsThePinsOfTheOnesThatStay()
        {
            EventEntryNode entry = NewEntry();
            entry.SetArguments([new EventArgument("amount", Int), new EventArgument("source", Text)]);
            NodeOutputDataPin first = entry.OutputDataPins[0];

            entry.SetArguments([new EventArgument("damage", Int)]);

            Assert.Same(first, entry.OutputDataPins.Single());
            Assert.Equal("damage", first.Name);
            Assert.Single(entry.InputTypePins);
        }

        [Theory]
        [InlineData("")]
        [InlineData("1st")]
        [InlineData("has space")]
        [InlineData("class")]
        public void AnInvalidIdentifierIsRefused(string name)
        {
            EventEntryNode entry = NewEntry();

            Assert.Throws<ArgumentException>(() => entry.SetArguments([new EventArgument(name, Int)]));
            Assert.Empty(entry.Arguments);
        }

        [Fact]
        public void ADuplicateNameIsRefused()
        {
            EventEntryNode entry = NewEntry();

            ArgumentException refused = Assert.Throws<ArgumentException>(
                () => entry.SetArguments([new EventArgument("amount", Int), new EventArgument("amount", Text)]));

            Assert.Contains("'amount'", refused.Message, StringComparison.Ordinal);
            Assert.Empty(entry.Arguments);
        }

        [Fact]
        public void AnOverrideEntryTakesItsArgumentsFromTheBaseMethod()
        {
            var overridden = new MethodSpecifier("Hit", [new MethodParameter("amount", Int, MethodParameterPassType.Default, false, null)],
                Array.Empty<BaseType>(), MethodModifiers.Virtual, MemberVisibility.Public, TypeSpecifier.FromType<object>(), Array.Empty<BaseType>());
            var entry = new EventEntryNode(new EventGraph("Events"), overridden);

            Assert.Equal([new EventArgument("amount", Int)], entry.Arguments);
            Assert.Throws<InvalidOperationException>(() => entry.SetArguments([]));
        }

        [Fact]
        public void TheDeclaredTypesSurviveTheTypeInferencePassOfALoad()
        {
            EventEntryNode entry = NewEntry();
            entry.SetArguments([new EventArgument("amount", Int), new EventArgument("source", Text)]);

            entry.OnMethodDeserialized();

            Assert.Equal([new EventArgument("amount", Int), new EventArgument("source", Text)], entry.Arguments);
        }

        [Fact]
        public void TheDeclaredArgumentsKeepTheTypesSetNotTheOnesInferred()
        {
            EventEntryNode entry = NewEntry();
            entry.AddArgument();
            entry.AddArgument();
            entry.SetArguments([new EventArgument("amount", Int), new EventArgument("source", Text)]);

            Assert.Equal([new EventArgument("amount", Int), new EventArgument("source", Text)], entry.DeclaredArguments);
        }
    }
}
