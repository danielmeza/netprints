using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Serialization;
using NetPrints.Serialization.Documents;
using NetPrints.Serialization.Mapping;
using NetPrints.Tests.Characterization;
using NetPrints.Translator;
using Xunit;

namespace NetPrints.Tests.Core
{
    /// <summary>
    /// A call can be pure only when it returns a value:
    /// a pure node is translated only when a consumer needs one of its outputs, so a call without one would
    /// vanish from the generated C#.
    /// </summary>
    public class CallMethodPurityTests
    {
        private static readonly TypeSpecifier IntType = TypeSpecifier.FromType<int>();

        private static readonly TypeSpecifier StringType = TypeSpecifier.FromType<string>();

        private static MethodSpecifier Specifier(string name, TypeSpecifier declaring, BaseType[] returns, params MethodParameter[] parameters) =>
            new(name, parameters, returns, MethodModifiers.Static, MemberVisibility.Public, declaring, []);

        private static MethodParameter Parameter(string name, TypeSpecifier type, MethodParameterPassType passType = MethodParameterPassType.Default) =>
            new(name, type, passType, false, null);

        private static MethodSpecifier WriteLine() =>
            Specifier("WriteLine", TypeSpecifier.FromType(typeof(Console)), [], Parameter("value", StringType));

        private static MethodSpecifier TryParse() =>
            Specifier("TryParse", IntType, [TypeSpecifier.FromType<bool>()],
                Parameter("s", StringType), Parameter("result", IntType, MethodParameterPassType.Out));

        private static MethodSpecifier VoidWithOut() =>
            Specifier("Fill", IntType, [], Parameter("result", IntType, MethodParameterPassType.Out));

        private static MethodSpecifier Max() =>
            Specifier("Max", TypeSpecifier.FromType(typeof(Math)), [IntType],
                Parameter("val1", IntType), Parameter("val2", IntType, MethodParameterPassType.In));

        [Fact]
        public void VoidCallCannotBePure()
        {
            var call = new CallMethodNode(new MethodGraph("M"), WriteLine());

            Assert.False(call.CanSetPure);
            Assert.Throws<InvalidOperationException>(() => call.IsPure = true);
            Assert.False(call.IsPure);
        }

        [Fact]
        public void VoidCallWithOnlyAnOutArgumentCannotBePure()
        {
            Assert.False(new CallMethodNode(new MethodGraph("M"), VoidWithOut()).CanSetPure);
        }

        [Fact]
        public void CallWithAReturnValueAndAnOutArgumentCanBePure()
        {
            var call = new CallMethodNode(new MethodGraph("M"), TryParse());

            Assert.True(call.CanSetPure);

            call.IsPure = true;

            Assert.True(call.IsPure);
        }

        [Fact]
        public void VoidCallWithCatchWiredCannotBePure()
        {
            var method = new MethodGraph("M");
            var call = new CallMethodNode(method, WriteLine());
            GraphUtil.ConnectExecPins(
                call.CatchPin ?? throw new InvalidOperationException("An impure call has a Catch pin."),
                method.MainReturnNode.ReturnPin);

            Assert.NotNull(call.ExceptionPin);
            Assert.False(call.CanSetPure);
        }

        [Fact]
        public void CallWithAReturnValueCanBePure()
        {
            var call = new CallMethodNode(new MethodGraph("M"), Max());

            Assert.True(call.CanSetPure);

            call.IsPure = true;

            Assert.True(call.IsPure);
        }

        [Fact]
        public void VoidCallIsEmittedBecauseItStaysImpure()
        {
            var method = new MethodGraph("Main") { Visibility = MemberVisibility.Public };
            var call = new CallMethodNode(method, WriteLine());
            LiteralNode text = LiteralNode.WithValue(method, "hi");
            GraphUtil.ConnectDataPins(text.ValuePin, call.ArgumentPins[0]);

            if (call.CanSetPure)
            {
                call.IsPure = true;
            }

            GraphUtil.ConnectExecPins(method.EntryNode.InitialExecutionPin, call.InputExecPins.Single());
            GraphUtil.ConnectExecPins(call.OutputExecPins[0], method.ReturnNodes.First().ReturnPin);

            string code = new ExecutionGraphTranslator(TranslationEnvironment.BuiltIn).Translate(method, true, []);

            Assert.Contains("WriteLine", code);
        }

        private static string TranslateTryParse(bool giveTheOutPinAnUnconnectedValue, MethodParameterPassType passType = MethodParameterPassType.Out)
        {
            var method = new MethodGraph("Main") { Visibility = MemberVisibility.Public };
            MethodSpecifier specifier = Specifier("TryParse", IntType, [TypeSpecifier.FromType<bool>()],
                Parameter("s", StringType), Parameter("result", IntType, passType));
            var call = new CallMethodNode(method, specifier);
            LiteralNode text = LiteralNode.WithValue(method, "1");
            GraphUtil.ConnectDataPins(text.ValuePin, call.ArgumentPins[0]);

            if (giveTheOutPinAnUnconnectedValue)
            {
                call.ArgumentPins[1].UnconnectedValue = 0;
            }

            GraphUtil.ConnectExecPins(method.EntryNode.InitialExecutionPin, call.InputExecPins.Single());
            GraphUtil.ConnectExecPins(call.OutputExecPins[0], method.ReturnNodes.First().ReturnPin);

            return new ExecutionGraphTranslator(TranslationEnvironment.BuiltIn).Translate(method, true, []);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void UnconnectedOutArgumentIsATranslationErrorNotBrokenCSharp(bool withUnconnectedValue)
        {
            var ex = Assert.Throws<TranslationException>(() => TranslateTryParse(withUnconnectedValue));

            Assert.Equal(TranslationDiagnosticCodes.UnsetRequiredInput, ex.Code);
            Assert.Contains("Connect a variable to out parameter 'result'", ex.Message);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void UnconnectedRefArgumentIsATranslationErrorNotBrokenCSharp(bool withUnconnectedValue)
        {
            var ex = Assert.Throws<TranslationException>(() => TranslateTryParse(withUnconnectedValue, MethodParameterPassType.Reference));

            Assert.Equal(TranslationDiagnosticCodes.UnsetRequiredInput, ex.Code);
            Assert.Contains("Connect a variable to ref parameter 'result'", ex.Message);
        }

        [Fact]
        public void LoadingPureOnACallThatCannotBePureLoadsItImpureAndReportsAnIssue()
        {
            var mapper = new DocumentMapper(new NodeDocumentConverterRegistry(NodeDocumentConverterRegistry.BuiltIn, []), NullLogger<DocumentMapper>.Instance);
            Project project = AllNodesFixtureFactory.CreateAllNodes("AllNodes.csproj");
            ClassDocument document = mapper.ToDocument(project.Classes.Single());

            MethodDocument main = document.Methods?.Single(m => m.Name == "Main") ?? throw new InvalidOperationException("No methods.");
            GraphDocument graph = main.Graph with
            {
                Nodes = main.Graph.Nodes.Select(n => n is CallMethodNodeDocument call ? call with { Pure = true } : n).ToList(),
            };
            ClassDocument pureDocument = document with
            {
                Methods = (document.Methods ?? []).Select(m => m.Name == "Main" ? m with { Graph = graph } : m).ToList(),
            };

            var issues = new List<DocumentIssue>();
            ClassGraph loaded = mapper.FromDocument(pureDocument, TestProjects.Create("P", "P"), issues, new DocumentId("C.netpc.json"));

            CallMethodNode writeLine = loaded.Methods.Single(m => m.Name == "Main").Nodes.OfType<CallMethodNode>().Single();
            Assert.False(writeLine.IsPure);
            Assert.Contains(issues, i => i.Code == DocumentIssue.PurityIgnored && i.Severity == DocumentIssueSeverity.Warning);
        }
    }
}
