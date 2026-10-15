using System;
using System.Collections.Generic;
using System.Linq;
using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Tests.Extensibility;
using NetPrints.Translator;
using Xunit;

namespace NetPrints.Tests.Core
{
    /// <summary>
    /// Review F R3 (FR-073): an argument edit of a custom event moves its callers onto the new signature
    /// (<see cref="SignatureChange.RetargetCallers"/>), so the class keeps compiling.
    /// </summary>
    public class SignatureChangeTests
    {
        private static readonly TypeSpecifier Int = TypeSpecifier.FromType<int>();
        private static readonly TypeSpecifier Long = TypeSpecifier.FromType<long>();
        private static readonly TypeSpecifier Text = TypeSpecifier.FromType<string>();

        private static (ClassGraph Class, EventEntryNode Entry, MethodGraph Run, CallMethodNode Call) EventWithACaller(params EventArgument[] arguments)
        {
            var cls = new ClassGraph { Name = "ProbeCombat", Namespace = "ReviewF", Visibility = MemberVisibility.Public };
            var events = new EventGraph("Events") { Class = cls };
            cls.EventGraphs.Add(events);
            var entry = new EventEntryNode(events, "OnHit");
            entry.SetArguments(arguments.Length == 0 ? [new EventArgument("amount", Int)] : arguments);

            var run = new MethodGraph("Run") { Class = cls, Visibility = MemberVisibility.Public, Modifiers = MethodModifiers.None };
            cls.Methods.Add(run);
            var specifier = new MethodSpecifier("OnHit",
                entry.Arguments.Select(argument => new MethodParameter(argument.Name, argument.Type, MethodParameterPassType.Default, false, null)),
                Array.Empty<BaseType>(), MethodModifiers.None, MemberVisibility.Public, cls.Type, Array.Empty<BaseType>());
            var call = new CallMethodNode(run, specifier);
            GraphUtil.ConnectExecPins(run.EntryNode.InitialExecutionPin, call.InputExecPins[0]);
            GraphUtil.ConnectExecPins(call.OutputExecPins[0], run.MainReturnNode.ReturnPin);
            return (cls, entry, run, call);
        }

        private static Action Change(ClassGraph cls, EventEntryNode entry, IReadOnlyList<EventArgument> arguments, IReadOnlyList<int> sources)
        {
            var oldKey = new MemberKey(MemberKind.Event, cls.Type, entry.EventName, entry.Arguments.Select(argument => (BaseType)argument.Type).ToList());
            entry.SetArguments(arguments);
            return SignatureChange.RetargetCallers([cls], oldKey, [.. arguments.Select(argument => new Named<BaseType>(argument.Name, argument.Type))], sources);
        }

        private static string Translate(ClassGraph cls) => new ClassTranslator(TranslationEnvironment.BuiltIn).TranslateClass(cls);

        private static CallMethodNode CallIn(NodeGraph graph) => graph.Nodes.OfType<CallMethodNode>().Single();

        [Fact]
        public void AddingAnArgumentKeepsTheCallerCompiling()
        {
            (ClassGraph cls, EventEntryNode entry, MethodGraph run, CallMethodNode call) = EventWithACaller();
            call.ArgumentPins[0].UnconnectedValue = 5;

            Change(cls, entry, [new EventArgument("amount", Int), new EventArgument("source", Text)], [0, -1]);

            CallMethodNode rebuilt = CallIn(run);
            Assert.NotSame(call, rebuilt);
            Assert.Equal(2, rebuilt.ArgumentPins.Count);
            Assert.Equal(5, rebuilt.ArgumentPins[0].UnconnectedValue);
            ExtensionTestSupport.Compile(ExtensionTestSupport.NewTempDirectory(), "SignatureAdded", Translate(cls));
        }

        [Fact]
        public void ARetypeFollowedByARenameStillRetargetsTheCall()
        {
            (ClassGraph cls, EventEntryNode entry, MethodGraph run, _) = EventWithACaller();

            Change(cls, entry, [new EventArgument("amount", Long)], [0]);
            MemberRename.RenameEvent([cls], entry, "OnDamage");

            CallMethodNode rebuilt = CallIn(run);
            Assert.Equal("OnDamage", rebuilt.MethodName);
            Assert.Equal(Long, rebuilt.ArgumentPins[0].PinType.Value);
            ExtensionTestSupport.Compile(ExtensionTestSupport.NewTempDirectory(), "SignatureRetyped", Translate(cls));
        }

        [Fact]
        public void ARenameOfAnArgumentKeepsTheCallCompiling()
        {
            (ClassGraph cls, EventEntryNode entry, MethodGraph run, _) = EventWithACaller();

            Change(cls, entry, [new EventArgument("damage", Int)], [0]);

            Assert.Equal("damage", CallIn(run).ArgumentPins[0].Name);
            ExtensionTestSupport.Compile(ExtensionTestSupport.NewTempDirectory(), "SignatureRenamedArgument", Translate(cls));
        }

        [Fact]
        public void ConnectionsFollowTheirArgumentAndARemovedOneIsDropped()
        {
            (ClassGraph cls, EventEntryNode entry, MethodGraph run, CallMethodNode call) = EventWithACaller(
                new EventArgument("amount", Int), new EventArgument("source", Text), new EventArgument("extra", Int));
            var amountSource = new CallMethodNode(run, StaticCall("GetAmount", Int));
            var textSource = new CallMethodNode(run, StaticCall("GetText", Text));
            var extraSource = new CallMethodNode(run, StaticCall("GetExtra", Int));
            GraphUtil.ConnectDataPins(amountSource.OutputDataPins[0], call.ArgumentPins[0]);
            GraphUtil.ConnectDataPins(textSource.OutputDataPins[0], call.ArgumentPins[1]);
            GraphUtil.ConnectDataPins(extraSource.OutputDataPins[0], call.ArgumentPins[2]);

            Change(cls, entry, [new EventArgument("source", Text), new EventArgument("amount", Int)], [1, 0]);

            CallMethodNode rebuilt = run.Nodes.OfType<CallMethodNode>().Single(node => node.MethodName == "OnHit");
            Assert.Same(textSource.OutputDataPins[0], rebuilt.ArgumentPins[0].IncomingPin);
            Assert.Same(amountSource.OutputDataPins[0], rebuilt.ArgumentPins[1].IncomingPin);
            Assert.Empty(extraSource.OutputDataPins[0].OutgoingPins);
        }

        [Fact]
        public void UndoPutsTheOriginalCallAndItsConnectionsBack()
        {
            (ClassGraph cls, EventEntryNode entry, MethodGraph run, CallMethodNode call) = EventWithACaller();
            var source = new CallMethodNode(run, StaticCall("GetAmount", Int));
            GraphUtil.ConnectDataPins(source.OutputDataPins[0], call.ArgumentPins[0]);

            Action undo = Change(cls, entry, [new EventArgument("amount", Int), new EventArgument("source", Text)], [0, -1]);
            undo();

            Assert.Same(call, run.Nodes.OfType<CallMethodNode>().Single(node => node.MethodName == "OnHit"));
            Assert.Same(source.OutputDataPins[0], call.ArgumentPins[0].IncomingPin);
            Assert.Same(run.EntryNode.InitialExecutionPin, call.InputExecPins[0].IncomingPins.Single());
            Assert.Same(run.MainReturnNode.ReturnPin, call.OutputExecPins[0].OutgoingPin);
        }

        [Fact]
        public void ADelegateToTheEventMovesToTheNewSignature()
        {
            (ClassGraph cls, EventEntryNode entry, MethodGraph run, CallMethodNode call) = EventWithACaller();
            var makeDelegate = new MakeDelegateNode(run, call.MethodSpecifier);

            Change(cls, entry, [new EventArgument("amount", Int), new EventArgument("source", Text)], [0, -1]);

            MakeDelegateNode rebuilt = run.Nodes.OfType<MakeDelegateNode>().Single();
            Assert.NotSame(makeDelegate, rebuilt);
            Assert.Equal([Int, Text], rebuilt.MethodSpecifier.ArgumentTypes);
        }

        private static MethodSpecifier StaticCall(string name, TypeSpecifier returns) =>
            new(name, [], [returns], MethodModifiers.Static, MemberVisibility.Public, TypeSpecifier.FromType<object>(), Array.Empty<BaseType>());
    }
}
