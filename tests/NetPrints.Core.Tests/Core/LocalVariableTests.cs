using System;
using System.Linq;
using NetPrints.Core;
using NetPrints.Graph;
using Xunit;

namespace NetPrintsUnitTests
{
    /// <summary>
    /// T084 (US5): <see cref="LocalVariable"/>, <see cref="ExecutionGraph.LocalVariables"/> and
    /// <see cref="ExecutionGraph.IsLocalNameAvailable"/>.
    /// </summary>
    public class LocalVariableTests
    {
        [Fact]
        public void ConstructorThrowsForAnInvalidIdentifier()
        {
            Assert.Throws<ArgumentException>(() => new LocalVariable("not valid", TypeSpecifier.FromType<int>()));
            Assert.Throws<ArgumentException>(() => new LocalVariable("class", TypeSpecifier.FromType<int>()));
        }

        [Fact]
        public void ConstructorAcceptsAValidIdentifier()
        {
            var local = new LocalVariable("count", TypeSpecifier.FromType<int>());

            Assert.Equal("count", local.Name);
            Assert.Equal(TypeSpecifier.FromType<int>(), local.Type);
        }

        [Fact]
        public void ToSpecifierIsAPrivateLocalWithNoDeclaringType()
        {
            var local = new LocalVariable("count", TypeSpecifier.FromType<int>());

            VariableSpecifier specifier = local.ToSpecifier();

            Assert.Equal("count", specifier.Name);
            Assert.Equal(TypeSpecifier.FromType<int>(), specifier.Type);
            Assert.Equal(VariableScope.Local, specifier.Scope);
            Assert.Null(specifier.DeclaringType);
            Assert.Equal(MemberVisibility.Private, specifier.GetterVisibility);
            Assert.Equal(MemberVisibility.Private, specifier.SetterVisibility);
            Assert.Equal(VariableModifiers.None, specifier.Modifiers);
        }

        [Fact]
        public void LocalVariablesStartsEmptyAndAcceptsAdditions()
        {
            var graph = new MethodGraph("Main");

            Assert.Empty(graph.LocalVariables);

            var local = new LocalVariable("count", TypeSpecifier.FromType<int>());
            graph.LocalVariables.Add(local);

            Assert.Same(local, Assert.Single(graph.LocalVariables));
        }

        [Fact]
        public void IsLocalNameAvailableRejectsAParameterName()
        {
            var graph = new MethodGraph("Main");
            ((MethodEntryNode)graph.EntryNode).AddArgument();
            string parameterName = graph.NamedArgumentTypes.Single().Name;

            Assert.False(graph.IsLocalNameAvailable(parameterName));
        }

        [Fact]
        public void IsLocalNameAvailableRejectsAnotherLocalButAllowsItsOwnName()
        {
            var graph = new MethodGraph("Main");
            var count = new LocalVariable("count", TypeSpecifier.FromType<int>());
            graph.LocalVariables.Add(count);

            Assert.False(graph.IsLocalNameAvailable("count"));
            Assert.True(graph.IsLocalNameAvailable("count", except: count));
            Assert.True(graph.IsLocalNameAvailable("other"));
        }

        [Fact]
        public void IsLocalNameAvailableRejectsAKeyword()
        {
            var graph = new MethodGraph("Main");

            Assert.False(graph.IsLocalNameAvailable("class"));
        }
    }
}
