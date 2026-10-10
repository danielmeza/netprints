using System;
using System.Linq;
using System.Threading.Tasks;
using NetPrints.Core;
using NetPrints.Graph;
using Xunit;

namespace NetPrints.Tests.Core
{
    /// <summary>The await node follows the result type of the task wired to it (Review F R12).</summary>
    public class AwaitNodeTests
    {
        private static MethodSpecifier Returning(string name, Type returns) =>
            new(name, [], [TypeSpecifier.FromType(returns)], MethodModifiers.Static, MemberVisibility.Public, TypeSpecifier.FromType<AwaitNodeTests>(), []);

        private static MethodSpecifier Consuming(Type parameter) =>
            new("Use", [new MethodParameter("value", TypeSpecifier.FromType(parameter), MethodParameterPassType.Default, false, null)],
                [], MethodModifiers.Static, MemberVisibility.Public, TypeSpecifier.FromType<AwaitNodeTests>(), []);

        private static MethodGraph NewMethod()
        {
            var cls = new ClassGraph { Name = "C", Namespace = "N" };
            var method = new MethodGraph("M") { Class = cls };
            cls.Methods.Add(method);
            return method;
        }

        [Fact]
        public void ChangingTheResultTypeWhileTheResultPinIsConnectedDisconnectsItWithoutThrowing()
        {
            MethodGraph method = NewMethod();
            var ints = new CallMethodNode(method, Returning("Ints", typeof(Task<int>)));
            var strings = new CallMethodNode(method, Returning("Strings", typeof(Task<string>)));
            var await = new AwaitNode(method);
            var consumer = new CallMethodNode(method, Consuming(typeof(int)));
            GraphUtil.ConnectDataPins(ints.ReturnValuePins[0], await.TaskPin);
            GraphUtil.ConnectDataPins(await.ResultPin ?? throw new InvalidOperationException("No result pin."), consumer.ArgumentPins[0]);

            GraphUtil.ConnectDataPins(strings.ReturnValuePins[0], await.TaskPin);

            Assert.Equal(TypeSpecifier.FromType<string>(), await.ResultPin?.PinType.Value);
            Assert.Empty(await.ResultPin?.OutgoingPins ?? []);
            Assert.Null(consumer.ArgumentPins[0].IncomingPin);
        }
    }
}
