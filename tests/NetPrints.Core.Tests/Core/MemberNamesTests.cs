using System;
using System.Linq;
using NetPrints.Core;
using NetPrints.Graph;
using Xunit;

namespace NetPrints.Tests.Core
{
    /// <summary>
    /// Review F R5 (FR-072): one clash check over methods, event entries, variables and the class name,
    /// used by every rename and by the names new members are given.
    /// </summary>
    public class MemberNamesTests
    {
        private static (ClassGraph Class, MethodGraph Method, EventEntryNode Entry, Variable Variable) NewClass()
        {
            var cls = new ClassGraph { Name = "Combat", Namespace = "Names" };
            var events = new EventGraph("Events") { Class = cls };
            cls.EventGraphs.Add(events);
            var entry = new EventEntryNode(events, "OnHit");
            var method = new MethodGraph("Run") { Class = cls };
            cls.Methods.Add(method);
            var variable = new Variable(cls, "Health", TypeSpecifier.FromType<int>(), null, null, VariableModifiers.None);
            cls.Variables.Add(variable);
            return (cls, method, entry, variable);
        }

        private static void AssertRefused(Action rename, string expected)
        {
            ArgumentException refused = Assert.Throws<ArgumentException>(rename);
            Assert.StartsWith(expected, refused.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AMethodCannotTakeAnEventVariableOrClassName()
        {
            (ClassGraph cls, MethodGraph method, _, _) = NewClass();

            AssertRefused(() => MemberRename.RenameMethod([cls], method, "OnHit"), "'OnHit' is already used by event 'OnHit'");
            AssertRefused(() => MemberRename.RenameMethod([cls], method, "Health"), "'Health' is already used by variable 'Health'");
            AssertRefused(() => MemberRename.RenameMethod([cls], method, "Combat"), "'Combat' is already used by class 'Combat'");
            Assert.Equal("Run", method.Name);
        }

        [Fact]
        public void AMethodMayShareANameWithAnotherMethodAsAnOverload()
        {
            (ClassGraph cls, MethodGraph method, _, _) = NewClass();
            cls.Methods.Add(new MethodGraph("Other") { Class = cls });

            MemberRename.RenameMethod([cls], method, "Other");

            Assert.Equal("Other", method.Name);
        }

        [Fact]
        public void AnEventCannotTakeAMethodVariableOrClassName()
        {
            (ClassGraph cls, _, EventEntryNode entry, _) = NewClass();

            AssertRefused(() => MemberRename.RenameEvent([cls], entry, "Run"), "'Run' is already used by method 'Run'");
            AssertRefused(() => MemberRename.RenameEvent([cls], entry, "Health"), "'Health' is already used by variable 'Health'");
            AssertRefused(() => MemberRename.RenameEvent([cls], entry, "Combat"), "'Combat' is already used by class 'Combat'");
            Assert.Equal("OnHit", entry.EventName);
        }

        [Fact]
        public void AVariableCannotTakeAMethodEventVariableOrClassName()
        {
            (ClassGraph cls, _, _, Variable variable) = NewClass();
            cls.Variables.Add(new Variable(cls, "Mana", TypeSpecifier.FromType<int>(), null, null, VariableModifiers.None));

            AssertRefused(() => MemberRename.RenameVariable([cls], variable, "Run"), "'Run' is already used by method 'Run'");
            AssertRefused(() => MemberRename.RenameVariable([cls], variable, "OnHit"), "'OnHit' is already used by event 'OnHit'");
            AssertRefused(() => MemberRename.RenameVariable([cls], variable, "Mana"), "'Mana' is already used by variable 'Mana'");
            AssertRefused(() => MemberRename.RenameVariable([cls], variable, "Combat"), "'Combat' is already used by class 'Combat'");
            Assert.Equal("Health", variable.Name);
        }

        [Fact]
        public void KeepingTheCurrentNameIsNotAClash()
        {
            (ClassGraph cls, MethodGraph method, EventEntryNode entry, Variable variable) = NewClass();

            MemberRename.RenameMethod([cls], method, "Run");
            MemberRename.RenameEvent([cls], entry, "OnHit");
            MemberRename.RenameVariable([cls], variable, "Health");
        }

        [Fact]
        public void NewMembersGetANameNoOtherKindUses()
        {
            (ClassGraph cls, _, _, _) = NewClass();

            Assert.Equal("Run2", MemberNames.Unique(cls, "Run"));
            Assert.Equal("OnHit2", MemberNames.Unique(cls, "OnHit"));
            Assert.Equal("Health2", MemberNames.Unique(cls, "Health"));
            Assert.Equal("Combat2", MemberNames.Unique(cls, "Combat"));
            Assert.Equal("Free", MemberNames.Unique(cls, "Free"));
        }
    }
}
