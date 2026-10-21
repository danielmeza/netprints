using System.Linq;
using NetPrints.Core;
using NetPrints.Graph;
using Xunit;

namespace NetPrints.Tests.Graph
{
    /// <summary>T035 (US2 scenarios 4 and 5): pin roles and the purity rule built on them.</summary>
    public class PinRoleTests
    {
        private static readonly TypeSpecifier IntType = TypeSpecifier.FromType<int>();

        private static readonly TypeSpecifier StringType = TypeSpecifier.FromType<string>();

        private static MethodSpecifier Specifier(string name, bool isStatic, BaseType[] returns, params MethodParameter[] parameters) =>
            new(name, parameters, returns, isStatic ? MethodModifiers.Static : MethodModifiers.None, MemberVisibility.Public, IntType, []);

        private static MethodParameter Parameter(string name, MethodParameterPassType passType = MethodParameterPassType.Default) =>
            new(name, IntType, passType, false, null);

        private static CallMethodNode Call(bool isStatic, BaseType[] returns, params MethodParameter[] parameters) =>
            new(new MethodGraph("M"), Specifier("Run", isStatic, returns, parameters));

        [Fact]
        public void ACallGivesItsPinsTheirRoles()
        {
            CallMethodNode call = Call(false, [IntType], Parameter("a"), Parameter("o", MethodParameterPassType.Out), Parameter("r", MethodParameterPassType.Reference));

            Assert.Equal(PinRole.Target, call.TargetPin.Role);
            Assert.Equal(PinRole.Argument, call.ArgumentPins[0].Role);
            Assert.Equal(PinRole.OutParam, call.ArgumentPins[1].Role);
            Assert.Equal(PinRole.OutParam, call.ArgumentPins[2].Role);
            Assert.Equal(PinRole.Result, call.OutputDataPins[0].Role);
            Assert.Equal(PinRole.OutParam, call.OutputDataPins[1].Role);
            Assert.Equal(PinRole.OutParam, call.OutputDataPins[2].Role);
        }

        [Fact]
        public void TheExceptionPinHasTheExceptionRole()
        {
            var method = new MethodGraph("M");
            var call = new CallMethodNode(method, Specifier("Run", true, [], Parameter("a")));
            GraphUtil.ConnectExecPins(call.CatchPin ?? throw new System.InvalidOperationException("No catch pin."), method.MainReturnNode.ExecPin);

            Assert.Equal(PinRole.Exception, call.ExceptionPin?.Role);
        }

        [Fact]
        public void PurityNeedsAResultOrAnOutParamOutput()
        {
            Assert.False(Call(true, [], Parameter("a")).CanSetPure);
            Assert.True(Call(true, [IntType], Parameter("a")).CanSetPure);
            Assert.True(Call(true, [], Parameter("o", MethodParameterPassType.Out)).CanSetPure);
            Assert.True(Call(true, [], Parameter("r", MethodParameterPassType.Reference)).CanSetPure);
        }

        [Fact]
        public void TheExceptionPinNeverCountsTowardPurity()
        {
            var method = new MethodGraph("M");
            var call = new CallMethodNode(method, Specifier("Run", true, [], Parameter("a")));
            GraphUtil.ConnectExecPins(call.CatchPin ?? throw new System.InvalidOperationException("No catch pin."), method.MainReturnNode.ExecPin);

            Assert.NotNull(call.ExceptionPin);
            Assert.False(call.CanSetPure);
        }

        [Fact]
        public void NodesThatCouldAlwaysBePureKeepOneResultOutput()
        {
            var method = new MethodGraph("M");
            var ctor = new ConstructorNode(method, new ConstructorSpecifier([], StringType));

            Assert.Equal(PinRole.Result, ctor.OutputDataPins[0].Role);
            Assert.True(ctor.CanSetPure);
            Assert.Equal(PinRole.Result, new TernaryNode(method).OutputDataPins.Single().Role);
            Assert.True(new TernaryNode(method).CanSetPure);
            Assert.True(new ExplicitCastNode(method).CanSetPure);
        }

        [Fact]
        public void NodesWithNoResultAreNotPure()
        {
            var method = new MethodGraph("M");

            Assert.False(new IfElseNode(method).CanSetPure);
            Assert.False(new ThrowNode(method).CanSetPure);
        }
    }
}
