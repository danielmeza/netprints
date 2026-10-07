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
    /// <see cref="MemberRename"/>: a renamed method or variable takes the nodes that use it along, matched by
    /// the member's identity, and the renamed class still compiles.
    /// </summary>
    public class MemberRenameTests
    {
        private static MethodSpecifier SpecifierOf(ClassGraph cls, string name, params TypeSpecifier[] parameters) =>
            new(name, parameters.Select(type => new MethodParameter("p", type, MethodParameterPassType.Default, false, null)),
                Array.Empty<BaseType>(), MethodModifiers.Static, MemberVisibility.Public, cls.Type, Array.Empty<BaseType>());

        private static MethodGraph StaticMethod(ClassGraph cls, string name)
        {
            var method = new MethodGraph(name) { Class = cls, Visibility = MemberVisibility.Public, Modifiers = MethodModifiers.Static };
            GraphUtil.ConnectExecPins(method.EntryNode.InitialExecutionPin, method.MainReturnNode.ReturnPin);
            cls.Methods.Add(method);
            return method;
        }

        private static ClassGraph NewClass(string name) => new() { Name = name, Namespace = "RenameTests" };

        [Fact]
        public void RenamingAMethodRetargetsOnlyItsOwnCalls()
        {
            ClassGraph cls = NewClass("A");
            ClassGraph other = NewClass("B");
            MethodGraph callee = StaticMethod(cls, "Callee");
            MethodGraph foreign = StaticMethod(other, "Callee");
            MethodGraph caller = StaticMethod(cls, "Caller");
            var toCallee = new CallMethodNode(caller, SpecifierOf(cls, "Callee"));
            var toForeign = new CallMethodNode(caller, SpecifierOf(other, "Callee"));
            var toOverload = new CallMethodNode(caller, SpecifierOf(cls, "Callee", TypeSpecifier.FromType<int>()));
            var delegateNode = new MakeDelegateNode(caller, SpecifierOf(cls, "Callee"));

            RenameResult result = MemberRename.RenameMethod([cls, other], callee, "Renamed");

            Assert.Equal("Renamed", callee.Name);
            Assert.Equal("Renamed", toCallee.MethodName);
            Assert.Equal("Renamed", delegateNode.MethodSpecifier.Name);
            Assert.Equal("Callee", toForeign.MethodName);
            Assert.Equal("Callee", toOverload.MethodName);
            Assert.Equal("Callee", foreign.Name);

            result.Undo();

            Assert.Equal("Callee", callee.Name);
            Assert.Equal("Callee", toCallee.MethodName);
            Assert.Equal("Callee", delegateNode.MethodSpecifier.Name);
        }

        [Fact]
        public void RenamingAVariableRetargetsOnlyItsOwnAccessors()
        {
            ClassGraph cls = NewClass("A");
            ClassGraph other = NewClass("B");
            var count = new Variable(cls, "Count", TypeSpecifier.FromType<int>(), null, null, VariableModifiers.Static);
            var foreign = new Variable(other, "Count", TypeSpecifier.FromType<int>(), null, null, VariableModifiers.Static);
            cls.Variables.Add(count);
            other.Variables.Add(foreign);
            MethodGraph caller = StaticMethod(cls, "Caller");
            var getter = new VariableGetterNode(caller, count.Specifier);
            var setter = new VariableSetterNode(caller, count.Specifier);
            var foreignGetter = new VariableGetterNode(caller, foreign.Specifier);
            var local = new VariableGetterNode(caller, new VariableSpecifier("Count", TypeSpecifier.FromType<int>(),
                MemberVisibility.Public, MemberVisibility.Public, null, VariableModifiers.None)
            { Scope = VariableScope.Local });

            RenameResult result = MemberRename.RenameVariable([cls, other], count, "Total");

            Assert.Equal("Total", count.Name);
            Assert.Equal("Total", getter.VariableName);
            Assert.Equal("Total", setter.VariableName);
            Assert.Equal("Count", foreignGetter.VariableName);
            Assert.Equal("Count", local.VariableName);

            result.Undo();

            Assert.Equal("Count", getter.VariableName);
            Assert.Equal("Count", setter.VariableName);
        }

        [Fact]
        public void TheRenamedClassStillCompiles()
        {
            ClassGraph cls = NewClass("RenameFixture");
            var count = new Variable(cls, "Count", TypeSpecifier.FromType<int>(), null, null, VariableModifiers.Static);
            cls.Variables.Add(count);
            MethodGraph callee = StaticMethod(cls, "Callee");
            MethodGraph run = StaticMethod(cls, "Run");
            var call = new CallMethodNode(run, SpecifierOf(cls, "Callee"));
            var getter = new VariableGetterNode(run, count.Specifier);
            var setter = new VariableSetterNode(run, count.Specifier);
            GraphUtil.ConnectExecPins(run.EntryNode.InitialExecutionPin, call.InputExecPins[0]);
            GraphUtil.ConnectExecPins(call.OutputExecPins[0], setter.InputExecPins[0]);
            GraphUtil.ConnectExecPins(setter.OutputExecPins[0], run.MainReturnNode.ReturnPin);
            GraphUtil.ConnectDataPins(getter.ValuePin, setter.NewValuePin);

            MemberRename.RenameMethod([cls], callee, "Renamed");
            MemberRename.RenameVariable([cls], count, "Total");

            string translated = new ClassTranslator(TranslationEnvironment.BuiltIn).TranslateClass(cls);
            Assert.Contains("Renamed()", translated);
            Assert.Contains("Total", translated);
            ExtensionTestSupport.Compile(ExtensionTestSupport.NewTempDirectory(), "RenameFixture", translated);
        }
    }
}
