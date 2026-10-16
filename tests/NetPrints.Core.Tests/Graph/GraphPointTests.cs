using System;
using NetPrints.Graph;
using Xunit;

namespace NetPrints.Tests.Graph
{
    /// <summary>
    /// Research R14, R31: <see cref="GraphPoint"/> is a public, stable Core type in <c>NetPrints.Graph</c>, so an
    /// extension that positions a node needs no editor reference.
    /// </summary>
    public class GraphPointTests
    {
        private const string GraphPointName = "NetPrints.Graph.GraphPoint";

        [Fact]
        public void GraphPointIsAPublicCoreType()
        {
            Type? type = typeof(Node).Assembly.GetType(GraphPointName);

            Assert.NotNull(type);
            Assert.True(type.IsPublic);
            Assert.True(type.IsValueType);
        }

        [Fact]
        public void ArithmeticIsComponentWise()
        {
            var a = new GraphPoint(1, 2);
            var b = new GraphPoint(10, 20);

            Assert.Equal(new GraphPoint(11, 22), a + b);
            Assert.Equal(new GraphPoint(9, 18), b - a);
            Assert.Equal(new GraphPoint(2, 4), a * 2);
            Assert.Equal(new GraphPoint(5.5, 11), GraphPoint.Lerp(a, b, 0.5));
            Assert.Equal(new GraphPoint(0, 0), GraphPoint.Zero);
        }
    }
}
