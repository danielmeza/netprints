using System;
using System.Linq;
using NetPrints.Core;
using NetPrints.Graph;
using Xunit;

namespace NetPrints.Tests.Core
{
    /// <summary>
    /// The OCP proof of <see cref="IMemberReferencingNode"/> (R1): a node kind unknown to <see cref="MemberRename"/>
    /// is retargeted by implementing the interface, with no change to <see cref="MemberRename"/>.
    /// </summary>
    public class MemberRenameExtensibilityTests
    {
        [Fact]
        public void ANodeKindThatImplementsTheInterfaceIsRetargetedWithNoChangeToMemberRename()
        {
            var cls = new ClassGraph { Name = "Contract", Namespace = "Contracts" };
            var callee = new MethodGraph("Callee") { Class = cls, Visibility = MemberVisibility.Public, Modifiers = MethodModifiers.Static };
            cls.Methods.Add(callee);
            var fx = new FxMemberReferenceNode(callee, new MemberKey(MemberKind.Method, cls.Type, "Callee", []));

            RenameResult result = MemberRename.RenameMethod([cls], callee, "Renamed");

            Assert.Equal("Renamed", fx.Target.Name);

            result.Undo();

            Assert.Equal("Callee", fx.Target.Name);
        }

        /// <summary>A node kind of an extension: it names a member and implements the role interface, nothing else.</summary>
        private sealed class FxMemberReferenceNode : Node, IMemberReferencingNode
        {
            public FxMemberReferenceNode(NodeGraph graph, MemberKey target)
                : base(graph)
            {
                Target = target;
            }

            public MemberKey Target { get; private set; }

            public bool RefersTo(MemberKey member) =>
                Target.Kind == member.Kind && Target.DeclaringType == member.DeclaringType && Target.Name == member.Name
                && Target.Parameters.SequenceEqual(member.Parameters);

            public Action Retarget(MemberKey member, string newName)
            {
                MemberKey before = Target;
                Target = before with { Name = newName };
                return () => Target = before;
            }
        }
    }
}
