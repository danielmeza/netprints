using System;
using NetPrints.Graph;
using Xunit;

namespace NetPrints.Tests.Graph
{
    /// <summary>Pins the 0.4.0 pin property renames: each property is named after its stored pin key.</summary>
    public class PinRenameTests
    {
        [Theory]
        [InlineData(typeof(IfElseNode), "ExecPin", "ExecutionPin")]
        [InlineData(typeof(ForLoopNode), "ExecPin", "ExecutionPin")]
        [InlineData(typeof(ThrowNode), "ExecPin", "ExecutionPin")]
        [InlineData(typeof(ReturnNode), "ExecPin", "ReturnPin")]
        [InlineData(typeof(ExecutionEntryNode), "ExecPin", "InitialExecutionPin")]
        [InlineData(typeof(EventEntryNode), "ExecPin", "InitialExecutionPin")]
        [InlineData(typeof(DefaultNode), "DefaultPin", "DefaultValuePin")]
        [InlineData(typeof(ClassReturnNode), "BaseTypePin", "SuperTypePin")]
        public void PinPropertyIsNamedAfterItsKey(Type nodeType, string newName, string oldName)
        {
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;

            Assert.NotNull(nodeType.GetProperty(newName, flags));
            Assert.Null(nodeType.GetProperty(oldName, flags));
        }

        [Fact]
        public void TypeOfNodeKeepsItsOverrideBecauseTypePinIsTaken()
        {
            Assert.NotNull(typeof(TypeOfNode).GetProperty("InputTypePin"));
            Assert.NotNull(typeof(TypeOfNode).GetProperty("TypePin"));
        }
    }
}
