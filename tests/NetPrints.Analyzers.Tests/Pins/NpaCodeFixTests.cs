using System.Threading.Tasks;
using NetPrints.Analyzers.Tests.Infrastructure;
using Xunit;

namespace NetPrints.Analyzers.Tests.Pins;

/// <summary>The NPA code fixes: make partial (NPA001), add the Pin attribute (NPA008) and add the marker (NPA009).</summary>
public class NpaCodeFixTests
{
    [Fact]
    public async Task NPA001MakesTheClassPartial()
    {
        string fixedText = await PinAnalyzerHarness.FixAsync(
            PinSamples.Header + """
            [NodeKind("sample.loop")]
            public sealed class LoopNode : Node
            {
                public LoopNode(NodeGraph graph) : base(graph)
                {
                    DeclarePins();
                }

                public partial NodeInputExecPin ExecutionPin { get; }
            }
            """,
            "NPA001");

        Assert.Equal(
            PinSamples.Header + """
            [NodeKind("sample.loop")]
            public sealed partial class LoopNode : Node
            {
                public LoopNode(NodeGraph graph) : base(graph)
                {
                    DeclarePins();
                }

                public partial NodeInputExecPin ExecutionPin { get; }
            }
            """,
            fixedText);
    }

    [Fact]
    public async Task NPA001MakesAPinPropertyAPartialGetOnlyProperty()
    {
        string fixedText = await PinAnalyzerHarness.FixAsync(
            PinSamples.Header + """
            [NodeKind("sample.loop")]
            public sealed partial class LoopNode : Node
            {
                public LoopNode(NodeGraph graph) : base(graph)
                {
                    DeclarePins();
                }

                public NodeInputExecPin ExecutionPin => InputExecPins[0];

                [Pin("Out")]
                public partial NodeOutputExecPin LoopPin { get; set; }
            }
            """,
            "NPA001");

        Assert.Equal(
            PinSamples.Header + """
            [NodeKind("sample.loop")]
            public sealed partial class LoopNode : Node
            {
                public LoopNode(NodeGraph graph) : base(graph)
                {
                    DeclarePins();
                }

                public partial NodeInputExecPin ExecutionPin { get; }

                [Pin("Out")]
                public partial NodeOutputExecPin LoopPin { get; }
            }
            """,
            fixedText);
    }

    [Fact]
    public async Task NPA008AddsAPinAttributeWithTheCurrentNameAsItsKey()
    {
        string fixedText = await PinAnalyzerHarness.FixAsync(
            PinSamples.Header + """
            [NodeKind("sample.loop")]
            public sealed partial class LoopNode : Node
            {
                public LoopNode(NodeGraph graph) : base(graph)
                {
                    DeclarePins();
                }

                [DefaultValue(3)]
                public partial NodeInputDataPin<int> Count { get; }

                public partial NodeInputExecPin Execution { get; }
            }
            """,
            "NPA008");

        Assert.Equal(
            PinSamples.Header + """
            [NodeKind("sample.loop")]
            public sealed partial class LoopNode : Node
            {
                public LoopNode(NodeGraph graph) : base(graph)
                {
                    DeclarePins();
                }

                [Pin("Count")]
                [DefaultValue(3)]
                public partial NodeInputDataPin<int> Count { get; }

                [Pin("Execution")]
                public partial NodeInputExecPin Execution { get; }
            }
            """,
            fixedText);
    }

    [Fact]
    public async Task NPA009AddsNodeKindToAConcreteClassAndNodeBaseToAnAbstractOne()
    {
        string fixedText = await PinAnalyzerHarness.FixAsync(
            PinSamples.Header + """
            public sealed partial class LoopNode : Node
            {
                public LoopNode(NodeGraph graph) : base(graph)
                {
                    DeclarePins();
                }

                public partial NodeInputExecPin ExecutionPin { get; }
            }

            public abstract partial class ExecBase : Node
            {
                protected ExecBase(NodeGraph graph) : base(graph)
                {
                }

                public partial NodeInputExecPin ExecutionPin { get; }
            }
            """,
            "NPA009");

        Assert.Equal(
            PinSamples.Header + """
            [NodeKind("TODO.kindId")]
            public sealed partial class LoopNode : Node
            {
                public LoopNode(NodeGraph graph) : base(graph)
                {
                    DeclarePins();
                }

                public partial NodeInputExecPin ExecutionPin { get; }
            }

            [NodeBase]
            public abstract partial class ExecBase : Node
            {
                protected ExecBase(NodeGraph graph) : base(graph)
                {
                }

                public partial NodeInputExecPin ExecutionPin { get; }
            }
            """,
            fixedText);
    }

    [Fact]
    public async Task NPA009QualifiesTheMarkerWhenTheGraphNamespaceIsNotImported()
    {
        string fixedText = await PinAnalyzerHarness.FixAsync(
            """
            namespace Sample;

            public abstract partial class ExecBase : NetPrints.Core.Node
            {
                protected ExecBase(NetPrints.Core.NodeGraph graph) : base(graph)
                {
                }

                public partial NetPrints.Graph.NodeInputExecPin ExecutionPin { get; }
            }
            """,
            "NPA009");

        Assert.Equal(
            """
            namespace Sample;

            [NetPrints.Graph.NodeBase]
            public abstract partial class ExecBase : NetPrints.Core.Node
            {
                protected ExecBase(NetPrints.Core.NodeGraph graph) : base(graph)
                {
                }

                public partial NetPrints.Graph.NodeInputExecPin ExecutionPin { get; }
            }
            """,
            fixedText);
    }
}
