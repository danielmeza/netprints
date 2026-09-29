using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Translator;
using Xunit;

namespace NetPrints.Tests
{
    public class ClassTranslatorTests
    {
        private ClassTranslator classTranslator;
        private MethodGraph stringLengthMethod;
        private MethodGraph mainMethod;

        private ClassGraph cls;

        [MemberNotNull(nameof(stringLengthMethod))]
        private void CreateStringLengthMethod()
        {
            // Create method
            stringLengthMethod = new MethodGraph("StringLength")
            {
                Class = cls,
                Modifiers = MethodModifiers.None
            };

            List<TypeNode> returnTypeNodes = new List<TypeNode>()
            {
                new TypeNode(stringLengthMethod, TypeSpecifier.FromType<int>()),
            };

            for (int i = 0; i < returnTypeNodes.Count; i++)
            {
                stringLengthMethod.MainReturnNode.AddReturnType();
                GraphUtil.ConnectTypePins(returnTypeNodes[i].OutputTypePins[0], stringLengthMethod.MainReturnNode.InputTypePins[i]);
            }

            TypeSpecifier stringType = TypeSpecifier.FromType<string>();
            TypeSpecifier intType = TypeSpecifier.FromType<int>();

            // Create nodes
            VariableGetterNode getStringNode = new VariableGetterNode(stringLengthMethod, new VariableSpecifier("testVariable", stringType, MemberVisibility.Public, MemberVisibility.Public, stringType, VariableModifiers.None));
            VariableGetterNode getLengthNode = new VariableGetterNode(stringLengthMethod, new VariableSpecifier("Length", intType, MemberVisibility.Public, MemberVisibility.Public, stringType, VariableModifiers.None));

            // Connect node execs
            GraphUtil.ConnectExecPins(stringLengthMethod.EntryNode.InitialExecutionPin, stringLengthMethod.ReturnNodes.First().ReturnPin);

            // Connect node data
            NodeInputDataPin? getLengthTargetPin = getLengthNode.TargetPin;
            Assert.NotNull(getLengthTargetPin);
            GraphUtil.ConnectDataPins(getStringNode.ValuePin, getLengthTargetPin);
            GraphUtil.ConnectDataPins(getLengthNode.ValuePin, stringLengthMethod.ReturnNodes.First().InputDataPins[0]);
        }

        [MemberNotNull(nameof(mainMethod))]
        private void CreateMainMethod()
        {
            mainMethod = new MethodGraph("Main")
            {
                Class = cls,
                Modifiers = MethodModifiers.Static
            };

            MethodSpecifier stringLengthSpecifier = new MethodSpecifier("StringLength", new MethodParameter[0], new List<TypeSpecifier>() { TypeSpecifier.FromType<int>() },
                MethodModifiers.None, MemberVisibility.Public, TypeSpecifier.FromType<string>(), Array.Empty<BaseType>());
            //MethodSpecifier writeConsoleSpecifier = typeof(Console).GetMethods().Single(m => m.Name == "WriteLine" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(string));
            TypeSpecifier stringType = TypeSpecifier.FromType<string>();
            MethodSpecifier writeConsoleSpecifier = new MethodSpecifier("WriteLine", new MethodParameter[] { new MethodParameter("argName", stringType, MethodParameterPassType.Default, false, null) }, new BaseType[0],
                MethodModifiers.None, MemberVisibility.Public, TypeSpecifier.FromType(typeof(Console)), new BaseType[0]);

            // Create nodes
            LiteralNode stringLiteralNode = LiteralNode.WithValue(mainMethod, "Hello World");
            VariableSetterNode setStringNode = new VariableSetterNode(mainMethod, new VariableSpecifier("testVariable", TypeSpecifier.FromType<string>(), MemberVisibility.Public, MemberVisibility.Public, cls.Type, VariableModifiers.None));
            CallMethodNode getStringLengthNode = new CallMethodNode(mainMethod, stringLengthSpecifier);
            CallMethodNode writeConsoleNode = new CallMethodNode(mainMethod, writeConsoleSpecifier);

            // Connect node execs
            GraphUtil.ConnectExecPins(mainMethod.EntryNode.InitialExecutionPin, setStringNode.InputExecPins[0]);
            GraphUtil.ConnectExecPins(setStringNode.OutputExecPins[0], getStringLengthNode.InputExecPins[0]);
            GraphUtil.ConnectExecPins(getStringLengthNode.OutputExecPins[0], writeConsoleNode.InputExecPins[0]);
            GraphUtil.ConnectExecPins(writeConsoleNode.OutputExecPins[0], mainMethod.ReturnNodes.First().InputExecPins[0]);

            // Connect node data
            GraphUtil.ConnectDataPins(stringLiteralNode.ValuePin, setStringNode.NewValuePin);
            GraphUtil.ConnectDataPins(getStringLengthNode.OutputDataPins[0], writeConsoleNode.ArgumentPins[0]);
        }

        public ClassTranslatorTests()
        {
            classTranslator = new ClassTranslator(TranslationEnvironment.BuiltIn);

            cls = new ClassGraph()
            {
                Name = "TestClass",
                Namespace = "TestNamespace",
            };

            CreateStringLengthMethod();
            CreateMainMethod();

            cls.Variables.Add(new Variable(cls, "testVariable", TypeSpecifier.FromType<string>(), null, null, VariableModifiers.None));
            cls.Methods.Add(stringLengthMethod);
            cls.Methods.Add(mainMethod);
        }

        [Fact]
        public void TestClassTranslation()
        {
            string translated = classTranslator.TranslateClass(cls);
        }

        /// <summary>
        /// Bug fix (implementation-notes.md "duplicate System.Object base"): an interface pin added
        /// (<see cref="ClassReturnNode.AddInterfacePin"/>) but left unconnected used to default to
        /// <c>System.Object</c> (<see cref="ClassGraph.AllBaseTypes"/>), duplicating the class's own
        /// implicit <c>System.Object</c> super type and producing CS1721. An unconnected interface pin
        /// means "no interface here", not "implements System.Object".
        /// </summary>
        [Fact]
        public void UnconnectedInterfacePinDoesNotDuplicateSystemObjectBase()
        {
            ClassGraph baseTypeFixture = new ClassGraph() { Name = "BaseTypeFixture", Namespace = "TestNamespace" };
            baseTypeFixture.ReturnNode.AddInterfacePin();

            string translated = classTranslator.TranslateClass(baseTypeFixture);

            Assert.DoesNotContain("System.Object, System.Object", translated);
        }

        private static MethodGraph CreateIntGetter(ClassGraph owner, string name, MemberVisibility visibility)
        {
            MethodGraph getter = new MethodGraph(name) { Class = owner, Visibility = visibility };

            TypeNode returnTypeNode = new TypeNode(getter, TypeSpecifier.FromType<int>());
            getter.MainReturnNode.AddReturnType();
            GraphUtil.ConnectTypePins(returnTypeNode.OutputTypePins[0], getter.MainReturnNode.InputTypePins[0]);

            LiteralNode literalNode = LiteralNode.WithValue(getter, 0);
            GraphUtil.ConnectExecPins(getter.EntryNode.InitialExecutionPin, getter.MainReturnNode.ReturnPin);
            GraphUtil.ConnectDataPins(literalNode.ValuePin, getter.MainReturnNode.InputDataPins[0]);

            return getter;
        }

        private static MethodGraph CreateIntSetter(ClassGraph owner, string name, MemberVisibility visibility)
        {
            MethodGraph setter = new MethodGraph(name) { Class = owner, Visibility = visibility };

            ((MethodEntryNode)setter.EntryNode).AddArgument();
            TypeNode argTypeNode = new TypeNode(setter, TypeSpecifier.FromType<int>());
            GraphUtil.ConnectTypePins(argTypeNode.OutputTypePins[0], setter.EntryNode.InputTypePins[0]);
            GraphUtil.ConnectExecPins(setter.EntryNode.InitialExecutionPin, setter.MainReturnNode.ReturnPin);

            return setter;
        }

        /// <summary>
        /// Bug fix (implementation-notes.md "private property with public accessors"): an accessor's
        /// visibility was emitted whenever it differed from the property's own (<c>!=</c>), instead of
        /// only when it is strictly more restrictive. A private property with public getter/setter
        /// methods (an impossible combination no real user would author, but one the model does not
        /// reject) produced <c>private ... { public get ... public set ... }</c>, CS0273/CS0274. The
        /// translator now drops a non-restrictive accessor modifier instead of emitting it.
        /// </summary>
        [Fact]
        public void PrivatePropertyWithPublicAccessorsEmitsNoAccessorModifier()
        {
            ClassGraph propertyFixture = new ClassGraph() { Name = "PropertyVisibilityFixture", Namespace = "TestNamespace" };
            MethodGraph getter = CreateIntGetter(propertyFixture, "get_Value", MemberVisibility.Public);
            MethodGraph setter = CreateIntSetter(propertyFixture, "set_Value", MemberVisibility.Public);
            Variable property = new Variable(propertyFixture, "Value", TypeSpecifier.FromType<int>(), getter, setter, VariableModifiers.None);

            string translated = classTranslator.TranslateVariable(property);

            Assert.DoesNotContain("public get", translated);
            Assert.DoesNotContain("public set", translated);
        }
    }
}
