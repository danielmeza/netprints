using System.Linq;
using System.Text.RegularExpressions;
using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Translator;
using Xunit;

namespace NetPrints.Tests.Graph
{
    /// <summary>T035 (US2 scenario 4): every <c>out</c> and <c>ref</c> parameter is also an output pin.</summary>
    public class OutRefPinTests
    {
        private static readonly TypeSpecifier IntType = TypeSpecifier.FromType<int>();

        private static readonly TypeSpecifier StringType = TypeSpecifier.FromType<string>();

        private static MethodSpecifier TryParse(MethodParameterPassType passType) =>
            new("TryParse", [new MethodParameter("s", StringType, MethodParameterPassType.Default, false, null), new MethodParameter("result", IntType, passType, false, null)],
                [TypeSpecifier.FromType<bool>()], MethodModifiers.Static, MemberVisibility.Public, IntType, []);

        [Theory]
        [InlineData(MethodParameterPassType.Out)]
        [InlineData(MethodParameterPassType.Reference)]
        public void EveryByReferenceParameterGetsAnOutputPinKeyedByItsName(MethodParameterPassType passType)
        {
            var call = new CallMethodNode(new MethodGraph("M"), TryParse(passType));

            NodeOutputDataPin pin = call.OutputDataPins.Single(p => p.Name == "result");

            Assert.Equal("out.data.result", PinKeys.For(pin));
            Assert.Equal(IntType, pin.PinType.Value);
            Assert.Equal(PinRole.OutParam, pin.Role);
        }

        [Fact]
        public void TheOutputPinsFollowTheReturnValuesAndTheReturnListExcludesThem()
        {
            var call = new CallMethodNode(new MethodGraph("M"), TryParse(MethodParameterPassType.Out));

            Assert.Equal(["Boolean", "result"], call.OutputDataPins.Select(p => p.Name));
            Assert.Single(call.ReturnValuePins);
        }

        [Fact]
        public void ACallWithoutByReferenceParametersHasNoExtraPins()
        {
            var call = new CallMethodNode(new MethodGraph("M"), TryParse(MethodParameterPassType.In));

            Assert.Single(call.OutputDataPins);
        }

        private static string Translate(bool pure)
        {
            var method = new MethodGraph("Main") { Visibility = MemberVisibility.Public };
            var local = new LocalVariable("count", IntType);
            method.LocalVariables.Add(local);
            var call = new CallMethodNode(method, TryParse(MethodParameterPassType.Out));
            var variable = new VariableGetterNode(method, local.ToSpecifier());
            GraphUtil.ConnectDataPins(LiteralNode.WithValue(method, "1").ValuePin, call.ArgumentPins[0]);
            GraphUtil.ConnectDataPins(variable.ValuePin, call.ArgumentPins[1]);

            if (pure)
            {
                call.IsPure = true;
            }
            else
            {
                GraphUtil.ConnectExecPins(method.EntryNode.ExecPin, call.InputExecPins.Single());
                GraphUtil.ConnectExecPins(call.OutputExecPins[0], method.ReturnNodes.First().ExecPin);
            }

            NodeOutputDataPin outPin = call.OutputDataPins.Single(p => p.Name == "result");
            var setter = new VariableSetterNode(method, local.ToSpecifier());
            if (pure)
            {
                GraphUtil.ConnectDataPins(outPin, setter.NewValuePin);
                GraphUtil.ConnectExecPins(method.EntryNode.ExecPin, setter.InputExecPins[0]);
                GraphUtil.ConnectExecPins(setter.OutputExecPins[0], method.ReturnNodes.First().ExecPin);
            }

            return new ExecutionGraphTranslator(TranslationEnvironment.BuiltIn).Translate(method, true, []);
        }

        [Fact]
        public void TheOutputPinCarriesTheValueAfterTheCall()
        {
            string code = Translate(pure: false);

            Assert.True(Regex.IsMatch(code, @"TryParse\(\w+, out (\w+)\);\s*varresult = \1;"), code);
        }

        [Fact]
        public void APureCallWithOutParametersIsEmittedWhenItsOutputIsUsed()
        {
            string code = Translate(pure: true);

            Assert.True(Regex.IsMatch(code, @"TryParse\(\w+, out (\w+)\);\s*varresult = \1;\s*count = varresult;"), code);
        }
    }
}
