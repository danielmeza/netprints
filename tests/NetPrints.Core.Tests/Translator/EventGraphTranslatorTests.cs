using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Translator;
using Xunit;

namespace NetPrints.Tests.Translator
{
    /// <summary>
    /// Sub-phase G (T079): <see cref="ExecutionGraphTranslator.TranslateEventEntry(EventGraph, EventEntryNode)"/>
    /// and <see cref="ClassTranslator"/>'s event methods, isolated from the full "EventGraphs" golden
    /// (<c>GoldenCSharpTests</c>/<c>RoundTripTests</c>), which covers the JSON pipeline end to end.
    /// </summary>
    public class EventGraphTranslatorTests
    {
        private static MethodSpecifier StaticMethod(string name, IEnumerable<MethodParameter> parameters, IEnumerable<BaseType> returnTypes) =>
            new(name, parameters, returnTypes, MethodModifiers.Static, MemberVisibility.Public, TypeSpecifier.FromType<object>(), new List<BaseType>());

        [Fact]
        public void TranslateEventEntryProducesAVoidMethodForACustomEventWithNoBody()
        {
            var eventGraph = new EventGraph("Events");
            var entry = new EventEntryNode(eventGraph, "OnStart");

            var translator = new ExecutionGraphTranslator(TranslationEnvironment.BuiltIn);
            string code = translator.TranslateEventEntry(eventGraph, entry);

            Assert.Contains("public void OnStart()", code);
        }

        // T103a: the null guards TranslateEventEntry gained in T011/T012's nullable rollout were not
        // covered by a test.
        [Fact]
        public void TranslateEventEntryThrowsForANullGraph()
        {
            var eventGraph = new EventGraph("Events");
            var entry = new EventEntryNode(eventGraph, "OnStart");
            var translator = new ExecutionGraphTranslator(TranslationEnvironment.BuiltIn);

            AssertTranslateEventEntryThrowsForNullArgument(translator, eventGraph: null, entry: entry);
        }

        [Fact]
        public void TranslateEventEntryThrowsForANullEntry()
        {
            var eventGraph = new EventGraph("Events");
            var translator = new ExecutionGraphTranslator(TranslationEnvironment.BuiltIn);

            AssertTranslateEventEntryThrowsForNullArgument(translator, eventGraph: eventGraph, entry: null);
        }

        /// <summary>
        /// Invokes <see cref="ExecutionGraphTranslator.TranslateEventEntry(EventGraph, EventEntryNode)"/>
        /// through reflection — its parameters are non-nullable, so a literal <see langword="null"/> (or a
        /// null-forgiving one) would not compile — and asserts the call throws <see cref="ArgumentNullException"/>.
        /// </summary>
        private static void AssertTranslateEventEntryThrowsForNullArgument(ExecutionGraphTranslator translator, EventGraph? eventGraph, EventEntryNode? entry)
        {
            MethodInfo method = typeof(ExecutionGraphTranslator).GetMethod(
                    nameof(ExecutionGraphTranslator.TranslateEventEntry), [typeof(EventGraph), typeof(EventEntryNode)])
                ?? throw new InvalidOperationException($"No two-argument '{nameof(ExecutionGraphTranslator.TranslateEventEntry)}' overload on {nameof(ExecutionGraphTranslator)}.");

            TargetInvocationException thrown = Assert.Throws<TargetInvocationException>(() => method.Invoke(translator, [eventGraph, entry]));

            Assert.IsType<ArgumentNullException>(thrown.InnerException);
        }

        // NPT001 (research.md K13): a node reachable only from a different entry of the same event
        // graph is never translated, so a data dependency on it is invalid.
        [Fact]
        public void TranslateEventEntryThrowsNpt001ForADataDependencyOnAnotherEntrysNode()
        {
            var cls = new ClassGraph { Name = "C" };
            var eventGraph = new EventGraph("Events") { Class = cls };
            cls.EventGraphs.Add(eventGraph);

            var entryA = new EventEntryNode(eventGraph, "A");
            var entryB = new EventEntryNode(eventGraph, "B");

            var producer = new CallMethodNode(eventGraph,
                StaticMethod("Produce", [], [TypeSpecifier.FromType<int>()]));
            GraphUtil.ConnectExecPins(entryB.InitialExecutionPin, producer.InputExecPins[0]);

            var consumer = new CallMethodNode(eventGraph,
                StaticMethod("Consume", [new MethodParameter("value", TypeSpecifier.FromType<int>(), MethodParameterPassType.Default, false, null)], []));
            GraphUtil.ConnectExecPins(entryA.InitialExecutionPin, consumer.InputExecPins[0]);

            // Invalid: consumer (reachable from A) depends on producer's output, but producer is only
            // reachable from B.
            GraphUtil.ConnectDataPins(producer.OutputDataPins[0], consumer.InputDataPins[0]);

            var translator = new ExecutionGraphTranslator(TranslationEnvironment.BuiltIn);
            var ex = Assert.Throws<TranslationException>(() => translator.TranslateEventEntry(eventGraph, entryA));

            Assert.Equal("NPT001", ex.Code);
            Assert.Equal(producer.Id, ex.NodeId);
        }

        // NPT002: an event name colliding with another entry's or a method's name is rejected.
        [Fact]
        public void TranslateClassThrowsNpt002ForADuplicateEventName()
        {
            var cls = new ClassGraph { Name = "C" };
            var eventGraph = new EventGraph("Events") { Class = cls };
            cls.EventGraphs.Add(eventGraph);

            _ = new EventEntryNode(eventGraph, "OnStart");
            _ = new EventEntryNode(eventGraph, "OnStart");

            var translator = new ClassTranslator(TranslationEnvironment.BuiltIn);
            var ex = Assert.Throws<TranslationException>(() => translator.TranslateClass(cls));

            Assert.Equal("NPT002", ex.Code);
        }

        [Fact]
        public void TranslateClassThrowsNpt002WhenAnEventNameCollidesWithAMethodName()
        {
            var cls = new ClassGraph { Name = "C" };
            var method = new MethodGraph("OnStart") { Class = cls };
            cls.Methods.Add(method);

            var eventGraph = new EventGraph("Events") { Class = cls };
            cls.EventGraphs.Add(eventGraph);
            _ = new EventEntryNode(eventGraph, "OnStart");

            var translator = new ClassTranslator(TranslationEnvironment.BuiltIn);
            var ex = Assert.Throws<TranslationException>(() => translator.TranslateClass(cls));

            Assert.Equal("NPT002", ex.Code);
        }

        // research.md K13 (open item): event methods are emitted in EventGraph.Entries order, i.e. node
        // order — unlike ClassGraph.Methods/Constructors, which have their own explicit collection.
        // Swapping two entries in Nodes swaps the generated methods' order.
        [Fact]
        public void SwappingTwoEntriesInNodeOrderSwapsTheGeneratedMethodOrder()
        {
            var cls = new ClassGraph { Name = "C" };
            var eventGraph = new EventGraph("Events") { Class = cls };
            cls.EventGraphs.Add(eventGraph);

            var first = new EventEntryNode(eventGraph, "First");
            var second = new EventEntryNode(eventGraph, "Second");
            Assert.Equal([first, second], eventGraph.Entries.ToList());

            var translator = new ClassTranslator(TranslationEnvironment.BuiltIn);
            string before = translator.TranslateClass(cls);
            Assert.True(before.IndexOf("void First()") < before.IndexOf("void Second()"));

            eventGraph.Nodes.Clear();
            eventGraph.Nodes.Add(second);
            eventGraph.Nodes.Add(first);
            Assert.Equal([second, first], eventGraph.Entries.ToList());

            string after = translator.TranslateClass(cls);
            Assert.True(after.IndexOf("void Second()") < after.IndexOf("void First()"));
        }
    }
}
