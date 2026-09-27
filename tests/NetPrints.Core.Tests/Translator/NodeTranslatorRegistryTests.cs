using System;
using System.Collections.Generic;
using System.Linq;
using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Translator;
using Xunit;

namespace NetPrints.Tests.Translator
{
    /// <summary>The node translation seam: registry, extension translators and <c>NPT006</c>.</summary>
    public class NodeTranslatorRegistryTests
    {
        private sealed class GreetNode : Node
        {
            public GreetNode(NodeGraph graph)
                : base(graph)
            {
                AddInputExecPin("Exec");
                AddOutputExecPin("Then");
            }

            public override string ToString() => "Greet";
        }

        private sealed class GreetTranslator : INodeTranslator
        {
            public void Translate(IExecutionTranslationContext context, Node node, int inputExecPinIndex)
            {
                context.AppendLine($"System.Console.WriteLine(\"hello from {context.Declaration?.Name}\");");
                context.WriteGotoOutputPinIfNecessary(node.OutputExecPins[0], node.InputExecPins[inputExecPinIndex]);
            }
        }

        private static (ClassGraph Class, MethodGraph Method, GreetNode Node) Build()
        {
            var cls = new ClassGraph { Name = "Host", Namespace = "Acme" };
            var method = new MethodGraph("Run") { Class = cls, Modifiers = MethodModifiers.Static };
            var greet = new GreetNode(method);
            GraphUtil.ConnectExecPins(method.EntryNode.InitialExecutionPin, greet.InputExecPins[0]);
            GraphUtil.ConnectExecPins(greet.OutputExecPins[0], method.ReturnNodes.First().InputExecPins[0]);
            cls.Methods.Add(method);
            return (cls, method, greet);
        }

        [Fact]
        public void BuiltInRegistryFindsBuiltInNodeTypesOnly()
        {
            Assert.NotNull(NodeTranslatorRegistry.BuiltIn.Find(typeof(CallMethodNode)));
            Assert.NotNull(NodeTranslatorRegistry.BuiltIn.Find(typeof(ForLoopNode)));
            Assert.Null(NodeTranslatorRegistry.BuiltIn.Find(typeof(GreetNode)));
        }

        [Fact]
        public void WithAddsATranslatorWithoutChangingTheOriginal()
        {
            var translator = new GreetTranslator();

            NodeTranslatorRegistry extended = NodeTranslatorRegistry.BuiltIn.With(typeof(GreetNode), translator);

            Assert.Same(translator, extended.Find(typeof(GreetNode)));
            Assert.Null(NodeTranslatorRegistry.BuiltIn.Find(typeof(GreetNode)));
            Assert.NotNull(extended.Find(typeof(CallMethodNode)));
        }

        [Fact]
        public void WithRejectsAnAlreadyRegisteredType()
        {
            Assert.Throws<ArgumentException>(() => NodeTranslatorRegistry.BuiltIn.With(typeof(CallMethodNode), new GreetTranslator()));
        }

        [Fact]
        public void NodeWithoutTranslatorFailsWithNpt006()
        {
            var (cls, _, greet) = Build();

            TranslationException failure = Assert.Throws<TranslationException>(
                () => new ClassTranslator(TranslationEnvironment.BuiltIn).TranslateClass(cls));

            Assert.Equal("NPT006", failure.Code);
            Assert.Contains(typeof(GreetNode).ToString(), failure.Message);
            Assert.Equal(greet.Id, failure.NodeId);
            Assert.Equal(GraphKeys.For(greet.Graph), failure.GraphKey);
        }

        [Fact]
        public void ExtensionTranslatorIsUsedForItsNodeType()
        {
            var (cls, _, _) = Build();
            var environment = new TranslationEnvironment(
                NodeTranslatorRegistry.BuiltIn.With(typeof(GreetNode), new GreetTranslator()), [], []);

            string code = new ClassTranslator(environment).TranslateClass(cls);

            Assert.Contains("System.Console.WriteLine(\"hello from Host\");", code);
        }

        [Fact]
        public void ClassGraphIsATypeDeclaration()
        {
            var cls = new ClassGraph { Name = "Host", Namespace = "Acme" };

            ITypeDeclaration declaration = cls;

            Assert.Equal("Acme.Host", declaration.FullName);
        }
    }
}
