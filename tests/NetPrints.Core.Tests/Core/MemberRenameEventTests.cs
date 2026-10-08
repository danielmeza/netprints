using System;
using System.Collections.Generic;
using System.Linq;
using NetPrints.Core;
using NetPrints.Graph;
using Xunit;

namespace NetPrints.Tests.Core
{
    /// <summary>
    /// US8 (FR-072): <see cref="MemberRename.RenameEvent"/> renames a custom event entry and every call to it,
    /// matched by identity, and refuses a name another method or entry of the class uses.
    /// </summary>
    public class MemberRenameEventTests
    {
        private static (ClassGraph Class, EventEntryNode Entry) ClassWithEvent(string eventName = "OnHit")
        {
            var cls = new ClassGraph { Name = "Contract", Namespace = "Contracts" };
            var events = new EventGraph("Events") { Class = cls };
            cls.EventGraphs.Add(events);
            var entry = new EventEntryNode(events, eventName);
            entry.SetArguments([new EventArgument("amount", TypeSpecifier.FromType<int>())]);
            return (cls, entry);
        }

        private static MethodGraph Caller(ClassGraph cls)
        {
            var method = new MethodGraph("Caller") { Class = cls, Visibility = MemberVisibility.Public, Modifiers = MethodModifiers.Static };
            cls.Methods.Add(method);
            return method;
        }

        private static CallMethodNode CallTo(NodeGraph caller, TypeSpecifier declaring, string name, params TypeSpecifier[] parameters) =>
            new(caller, new MethodSpecifier(name, parameters.Select(type => new MethodParameter("p", type, MethodParameterPassType.Default, false, null)),
                Array.Empty<BaseType>(), MethodModifiers.None, MemberVisibility.Public, declaring, Array.Empty<BaseType>()));

        [Fact]
        public void RenamingAnEventRetargetsOnlyItsOwnCalls()
        {
            (ClassGraph cls, EventEntryNode entry) = ClassWithEvent();
            MethodGraph caller = Caller(cls);
            CallMethodNode toEvent = CallTo(caller, cls.Type, "OnHit", TypeSpecifier.FromType<int>());
            CallMethodNode otherOverload = CallTo(caller, cls.Type, "OnHit");
            CallMethodNode otherClass = CallTo(caller, TypeSpecifier.FromType<object>(), "OnHit", TypeSpecifier.FromType<int>());

            RenameResult result = MemberRename.RenameEvent([cls], entry, "OnDamage");

            Assert.Equal("OnDamage", entry.EventName);
            Assert.Equal("OnDamage", toEvent.MethodName);
            Assert.Equal("OnHit", otherOverload.MethodName);
            Assert.Equal("OnHit", otherClass.MethodName);

            result.Undo();

            Assert.Equal("OnHit", entry.EventName);
            Assert.Equal("OnHit", toEvent.MethodName);
        }

        [Fact]
        public void ANameUsedByAMethodIsRefused()
        {
            (ClassGraph cls, EventEntryNode entry) = ClassWithEvent();
            Caller(cls);

            ArgumentException refused = Assert.Throws<ArgumentException>(() => MemberRename.RenameEvent([cls], entry, "Caller"));

            Assert.StartsWith("'Caller' is already used by method 'Caller'", refused.Message, StringComparison.Ordinal);
            Assert.Equal("OnHit", entry.EventName);
        }

        [Fact]
        public void ANameUsedByAnotherEntryIsRefused()
        {
            (ClassGraph cls, EventEntryNode entry) = ClassWithEvent();
            _ = new EventEntryNode(cls.EventGraphs[0], "OnMiss");

            ArgumentException refused = Assert.Throws<ArgumentException>(() => MemberRename.RenameEvent([cls], entry, "OnMiss"));

            Assert.StartsWith("'OnMiss' is already used by event 'OnMiss'", refused.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AnOverrideEntryCannotBeRenamed()
        {
            var cls = new ClassGraph { Name = "Contract", Namespace = "Contracts" };
            var events = new EventGraph("Events") { Class = cls };
            cls.EventGraphs.Add(events);
            var overridden = new MethodSpecifier("Reset", [], Array.Empty<BaseType>(), MethodModifiers.Virtual, MemberVisibility.Public, TypeSpecifier.FromType<object>(), Array.Empty<BaseType>());
            var entry = new EventEntryNode(events, overridden);

            Assert.Throws<InvalidOperationException>(() => MemberRename.RenameEvent([cls], entry, "Other"));
        }
    }
}
