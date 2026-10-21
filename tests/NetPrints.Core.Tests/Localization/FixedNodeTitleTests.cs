using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NetPrints.Core;
using NetPrints.Graph;
using Xunit;

namespace NetPrints.Tests.Localization
{
    /// <summary>FR-027, T171: a node with a fixed title shows its neutral <c>Node.&lt;kind&gt;.Title</c> text, which is the text the node showed before the resources were used.</summary>
    public class FixedNodeTitleTests
    {
        public static TheoryData<string, string> FixedTitles()
        {
            var cls = new ClassGraph { Name = "C", Namespace = "N" };
            var method = new MethodGraph("M") { Class = cls };
            var typeGraph = new TypeGraph();
            (string Kind, Node Node)[] nodes =
            [
                ("await", new AwaitNode(method)),
                ("classReturn", new ClassReturnNode(cls)),
                ("forLoop", new ForLoopNode(method)),
                ("ifElse", new IfElseNode(method)),
                ("return", new ReturnNode(method)),
                ("throw", new ThrowNode(method)),
                ("typeOf", new TypeOfNode(method)),
                ("typeReturn", new TypeReturnNode(typeGraph)),
            ];

            var data = new TheoryData<string, string>();
            foreach ((string kind, Node node) in nodes)
            {
                data.Add(kind, node.ToString());
            }

            return data;
        }

        [Fact]
        public void OnlyNodesWithAFixedTextAreMarkedFixed()
        {
            var cls = new ClassGraph { Name = "C", Namespace = "N" };
            var method = new MethodGraph("M") { Class = cls };
            Node[] fixedTitle = [new AwaitNode(method), new ClassReturnNode(cls), new ForLoopNode(method), new IfElseNode(method), new ReturnNode(method), new ThrowNode(method), new TypeOfNode(method), new TypeReturnNode(new TypeGraph())];
            Node[] dynamicTitle = [new ExplicitCastNode(method), new DefaultNode(method), method.EntryNode];

            Assert.All(fixedTitle, node => Assert.True(node.TitleIsFixed, node.GetType().Name));
            Assert.All(dynamicTitle, node => Assert.False(node.TitleIsFixed, node.GetType().Name));
        }

        [Theory]
        [MemberData(nameof(FixedTitles))]
        public void TheNeutralTitleIsTheTextTheNodeShowed(string kind, string shown)
        {
            string? neutral = CoreStrings.Resources.GetString("Node." + kind + ".Title", CultureInfo.InvariantCulture);

            Assert.Equal(shown, neutral);
        }
    }
}
