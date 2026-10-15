using System.Linq;
using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Translator;
using Xunit;

namespace NetPrints.Tests.Translator
{
    /// <summary>
    /// T085 (US5): <see cref="ExecutionGraphTranslator"/>'s local-variable handling (data-model.md §3),
    /// isolated from the full "Locals" golden (<c>GoldenCSharpTests</c>/<c>RoundTripTests</c>) and the
    /// build/run test, which cover the JSON pipeline and a real compile end to end.
    /// </summary>
    public class LocalVariableTranslatorTests
    {
        private static MethodGraph NewMethodWithLocal(out LocalVariable local)
        {
            var method = new MethodGraph("Main") { Visibility = MemberVisibility.Public };
            local = new LocalVariable("count", TypeSpecifier.FromType<int>());
            method.LocalVariables.Add(local);
            return method;
        }

        [Fact]
        public void LocalIsDeclaredFirstAfterTheVariablesHeader()
        {
            MethodGraph method = NewMethodWithLocal(out _);

            var translator = new ExecutionGraphTranslator(TranslationEnvironment.BuiltIn);
            string code = translator.Translate(method, true, []);

            int headerIndex = code.IndexOf("// Variables");
            int localIndex = code.IndexOf("System.Int32 count = default(System.Int32);");
            Assert.True(headerIndex >= 0 && localIndex > headerIndex,
                $"Expected 'System.Int32 count = default(System.Int32);' right after the '// Variables' header. Code:\n{code}");
        }

        [Fact]
        public void SetterAssignsTheBareNameWithNoTargetOrDot()
        {
            MethodGraph method = NewMethodWithLocal(out LocalVariable local);

            var setter = new VariableSetterNode(method, local.ToSpecifier());
            LiteralNode literal = LiteralNode.WithValue(method, 5);
            GraphUtil.ConnectDataPins(literal.ValuePin, setter.NewValuePin);
            GraphUtil.ConnectExecPins(method.EntryNode.InitialExecutionPin, setter.InputExecPins[0]);
            GraphUtil.ConnectExecPins(setter.OutputExecPins[0], method.ReturnNodes.First().ReturnPin);

            var translator = new ExecutionGraphTranslator(TranslationEnvironment.BuiltIn);
            string code = translator.Translate(method, true, []);

            Assert.Contains("count = ", code);
            Assert.DoesNotContain("this.count", code);
            Assert.DoesNotContain(".count", code);
        }

        [Fact]
        public void IndexerSetterAssignsTheNewValueToTheIndexedElement()
        {
            var method = new MethodGraph("Main") { Visibility = MemberVisibility.Public };
            var indexer = new VariableSpecifier("this[]", TypeSpecifier.FromType<int>(), MemberVisibility.Public,
                MemberVisibility.Public, TypeSpecifier.FromType<System.Collections.Generic.List<int>>(), VariableModifiers.None);

            var setter = new VariableSetterNode(method, indexer);
            LiteralNode index = LiteralNode.WithValue(method, 7);
            LiteralNode value = LiteralNode.WithValue(method, 5);
            GraphUtil.ConnectDataPins(index.ValuePin, setter.IndexPin ?? throw new System.InvalidOperationException("No index pin."));
            GraphUtil.ConnectDataPins(value.ValuePin, setter.NewValuePin);
            GraphUtil.ConnectExecPins(method.EntryNode.InitialExecutionPin, setter.InputExecPins[0]);
            GraphUtil.ConnectExecPins(setter.OutputExecPins[0], method.ReturnNodes.First().ReturnPin);

            var translator = new ExecutionGraphTranslator(TranslationEnvironment.BuiltIn);
            string code = translator.Translate(method, true, []);

            var assignment = System.Text.RegularExpressions.Regex.Match(code, @"this\[(\w+)\] = (\w+);");
            Assert.True(assignment.Success, $"Expected an indexer assignment. Code:\n{code}");
            Assert.Contains($"{assignment.Groups[1].Value} = 7;", code);
            Assert.Contains($"{assignment.Groups[2].Value} = 5;", code);
        }

        [Fact]
        public void Npt004WhenALocalNameMatchesAParameter()
        {
            var method = new MethodGraph("Main") { Visibility = MemberVisibility.Public };
            ((MethodEntryNode)method.EntryNode).AddArgument();
            string parameterName = method.NamedArgumentTypes.Single().Name;
            method.LocalVariables.Add(new LocalVariable(parameterName, TypeSpecifier.FromType<int>()));

            var translator = new ExecutionGraphTranslator(TranslationEnvironment.BuiltIn);
            var ex = Assert.Throws<TranslationException>(() => translator.Translate(method, true, []));

            Assert.Equal("NPT004", ex.Code);
        }

        [Fact]
        public void Npt004WhenTwoLocalsShareAName()
        {
            var method = new MethodGraph("Main") { Visibility = MemberVisibility.Public };
            method.LocalVariables.Add(new LocalVariable("count", TypeSpecifier.FromType<int>()));
            method.LocalVariables.Add(new LocalVariable("count", TypeSpecifier.FromType<int>()));

            var translator = new ExecutionGraphTranslator(TranslationEnvironment.BuiltIn);
            var ex = Assert.Throws<TranslationException>(() => translator.Translate(method, true, []));

            Assert.Equal("NPT004", ex.Code);
        }

        [Fact]
        public void Npt004WhenALocalIsRenamedToAKeywordOutsideTheEditorsGate()
        {
            MethodGraph method = NewMethodWithLocal(out LocalVariable local);

            // LocalVariable.Name has no setter-time validation (only the constructor validates); this
            // simulates a rename that bypassed ExecutionGraph.IsLocalNameAvailable's gate (T087).
            local.Name = "class";

            var translator = new ExecutionGraphTranslator(TranslationEnvironment.BuiltIn);
            var ex = Assert.Throws<TranslationException>(() => translator.Translate(method, true, []));

            Assert.Equal("NPT004", ex.Code);
        }
    }
}
