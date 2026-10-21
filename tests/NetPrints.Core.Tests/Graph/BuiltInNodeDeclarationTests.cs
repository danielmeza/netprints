using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Serialization.Documents;
using Xunit;

namespace NetPrints.Tests.Graph
{
    /// <summary>The built-in node kinds that declare their pins by convention (FR-010, SC-002).</summary>
    public class BuiltInNodeDeclarationTests
    {
        public static TheoryData<Type, string, string[]> Declared => new()
        {
            { typeof(IfElseNode), BuiltInNodeKinds.IfElse, ["in.exec.Exec", "in.data.Condition", "out.exec.True", "out.exec.False"] },
            {
                typeof(ForLoopNode),
                BuiltInNodeKinds.ForLoop,
                ["in.exec.Exec", "in.exec.Continue", "out.exec.Loop", "out.exec.Completed", "in.data.InitialIndex", "in.data.MaxIndex", "out.data.Index"]
            },
            { typeof(ThrowNode), BuiltInNodeKinds.Throw, ["in.exec.Exec", "in.data.Exception"] },
            { typeof(TypeReturnNode), BuiltInNodeKinds.TypeReturn, ["in.type.Type"] },
            { typeof(TypeOfNode), BuiltInNodeKinds.TypeOf, ["in.type.Type", "out.data.Type"] },
            { typeof(DefaultNode), BuiltInNodeKinds.Default, ["in.type.Type", "out.data.Default"] },
            { typeof(MakeArrayTypeNode), BuiltInNodeKinds.MakeArrayType, ["in.type.ElementType", "out.type.ArrayType"] },
            { typeof(ClassReturnNode), BuiltInNodeKinds.ClassReturn, ["in.type.BaseType"] },
            { typeof(ReturnNode), BuiltInNodeKinds.Return, ["in.exec.Exec"] },
            { typeof(EventEntryNode), BuiltInNodeKinds.EventEntry, ["out.exec.Exec"] },
        };

        private static readonly string[] ImperativeKinds =
        [
            nameof(AwaitNode),
            nameof(CallMethodNode),
            nameof(ConstructorNode),
            nameof(ExplicitCastNode),
            nameof(LiteralNode),
            nameof(MakeArrayNode),
            nameof(MakeDelegateNode),
            nameof(RerouteNode),
            nameof(TernaryNode),
            nameof(TypeNode),
            nameof(VariableGetterNode),
            nameof(VariableSetterNode),
        ];

        [Theory]
        [MemberData(nameof(Declared))]
        public void KindDeclaresItsPinsInOrder(Type kind, string id, string[] layout)
        {
            Assert.Equal(id, kind.GetCustomAttribute<NodeKindAttribute>()?.Id);
            Assert.Equal(layout, LayoutOf(kind));
        }

        private static string[] LayoutOf(Type kind) => kind.GetCustomAttribute<NodePinLayoutAttribute>()?.Pins ?? [];

        [Theory]
        [InlineData(typeof(MethodEntryNode), BuiltInNodeKinds.MethodEntry)]
        [InlineData(typeof(ConstructorEntryNode), BuiltInNodeKinds.ConstructorEntry)]
        public void EntryKindsInheritTheirExecutionPin(Type kind, string id)
        {
            Assert.Equal(id, kind.GetCustomAttribute<NodeKindAttribute>()?.Id);
            Assert.Equal(["out.exec.Exec"], LayoutOf(typeof(ExecutionEntryNode)));
            Assert.NotNull(typeof(ExecutionEntryNode).GetCustomAttribute<NodeBaseAttribute>());
        }

        [Fact]
        public void KindIdsAreUnique()
        {
            List<string> ids = typeof(Node).Assembly.GetTypes()
                .Select(type => type.GetCustomAttribute<NodeKindAttribute>()?.Id)
                .OfType<string>()
                .ToList();

            Assert.NotEmpty(ids);
            Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
        }

        [Fact]
        public void EveryOtherBuiltInKindIsListedAsImperative()
        {
            string[] undeclared = typeof(Node).Assembly.GetTypes()
                .Where(type => type.IsSubclassOf(typeof(Node)) && !type.IsAbstract && type.GetCustomAttribute<NodeKindAttribute>() is null)
                .Select(type => type.Name)
                .Order(StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(ImperativeKinds.Order(StringComparer.Ordinal), undeclared);
        }
    }
}
