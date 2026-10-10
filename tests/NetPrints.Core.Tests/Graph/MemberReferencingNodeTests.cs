using System;
using System.Collections.Generic;
using System.Linq;
using NetPrints.Core;
using NetPrints.Graph;

namespace NetPrints.Tests.Graph
{
    internal static class MemberKeys
    {
        public static readonly TypeSpecifier Declaring = new("Contracts.Contract");

        public static MethodSpecifier Method(string name, params TypeSpecifier[] parameters) =>
            new(name, parameters.Select(type => new MethodParameter("p", type, MethodParameterPassType.Default, false, null)),
                Array.Empty<BaseType>(), MethodModifiers.Static, MemberVisibility.Public, Declaring, Array.Empty<BaseType>());

        public static VariableSpecifier Variable(string name) =>
            new(name, TypeSpecifier.FromType<int>(), MemberVisibility.Public, MemberVisibility.Public, Declaring, VariableModifiers.Static);
    }

    public sealed class CallMethodNodeContract : MemberReferencingNodeContract<CallMethodNode>
    {
        protected override MemberKey OldKey { get; } = new(MemberKind.Method, MemberKeys.Declaring, "Callee", []);

        protected override CallMethodNode CreateNode(NodeGraph graph) => new(graph, MemberKeys.Method("Callee"));

        protected override string TargetOf(CallMethodNode node) => node.MethodSpecifier.Name;
    }

    public sealed class CallEventNodeContract : MemberReferencingNodeContract<CallMethodNode>
    {
        protected override MemberKey OldKey { get; } = new(MemberKind.Event, MemberKeys.Declaring, "OnHit", [TypeSpecifier.FromType<int>()]);

        protected override CallMethodNode CreateNode(NodeGraph graph) => new(graph, MemberKeys.Method("OnHit", TypeSpecifier.FromType<int>()));

        protected override string TargetOf(CallMethodNode node) => node.MethodSpecifier.Name;
    }

    public sealed class MakeDelegateNodeContract : MemberReferencingNodeContract<MakeDelegateNode>
    {
        protected override MemberKey OldKey { get; } = new(MemberKind.Method, MemberKeys.Declaring, "Callee", []);

        protected override MakeDelegateNode CreateNode(NodeGraph graph) => new(graph, MemberKeys.Method("Callee"));

        protected override string TargetOf(MakeDelegateNode node) => node.MethodSpecifier.Name;
    }

    public sealed class MakeDelegateEventNodeContract : MemberReferencingNodeContract<MakeDelegateNode>
    {
        protected override MemberKey OldKey { get; } = new(MemberKind.Event, MemberKeys.Declaring, "OnHit", [TypeSpecifier.FromType<int>()]);

        protected override MakeDelegateNode CreateNode(NodeGraph graph) => new(graph, MemberKeys.Method("OnHit", TypeSpecifier.FromType<int>()));

        protected override string TargetOf(MakeDelegateNode node) => node.MethodSpecifier.Name;
    }

    public sealed class VariableGetterNodeContract : MemberReferencingNodeContract<VariableGetterNode>
    {
        protected override MemberKey OldKey { get; } = new(MemberKind.Variable, MemberKeys.Declaring, "Count", []);

        protected override VariableGetterNode CreateNode(NodeGraph graph) => new(graph, MemberKeys.Variable("Count"));

        protected override string TargetOf(VariableGetterNode node) => node.VariableName;
    }

    public sealed class VariableSetterNodeContract : MemberReferencingNodeContract<VariableSetterNode>
    {
        protected override MemberKey OldKey { get; } = new(MemberKind.Variable, MemberKeys.Declaring, "Count", []);

        protected override VariableSetterNode CreateNode(NodeGraph graph) => new(graph, MemberKeys.Variable("Count"));

        protected override string TargetOf(VariableSetterNode node) => node.VariableName;
    }
}
