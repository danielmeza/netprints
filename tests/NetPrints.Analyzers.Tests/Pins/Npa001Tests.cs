using System.Threading.Tasks;
using NetPrints.Analyzers.Tests.Infrastructure;
using Xunit;

namespace NetPrints.Analyzers.Tests.Pins;

/// <summary>NPA001: a pin property or its class is not partial, the property is not get-only, or its type does not match the attribute.</summary>
public class Npa001Tests
{
    private const string Id = "NPA001";

    [Fact]
    public async Task AWellFormedNodeIsNotReported()
    {
        await PinAnalyzerHarness.AssertDiagnosticsAsync(
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
                public partial NodeOutputDataPin<int> IndexPin { get; }
            }
            """,
            Id);
    }

    [Fact]
    public async Task ANodeClassThatIsNotPartialIsReportedAtItsName()
    {
        await PinAnalyzerHarness.AssertDiagnosticsAsync(
            PinSamples.Header + """
            [NodeKind("sample.loop")]
            public sealed class {|NPA001:LoopNode|} : Node
            {
                public LoopNode(NodeGraph graph) : base(graph)
                {
                }

                public partial NodeInputExecPin ExecutionPin { get; }
            }
            """,
            Id);
    }

    [Fact]
    public async Task APinPropertyThatIsNotPartialIsReportedAtItsName()
    {
        await PinAnalyzerHarness.AssertDiagnosticsAsync(
            PinSamples.Header + """
            [NodeKind("sample.loop")]
            public sealed partial class LoopNode : Node
            {
                public LoopNode(NodeGraph graph) : base(graph)
                {
                    DeclarePins();
                }

                public partial NodeInputExecPin ExecutionPin { get; }

                public NodeOutputExecPin {|NPA001:LoopPin|} => OutputExecPins[0];
            }
            """,
            Id);
    }

    [Fact]
    public async Task APinPropertyWithASetterIsReportedAtItsName()
    {
        await PinAnalyzerHarness.AssertDiagnosticsAsync(
            PinSamples.Header + """
            [NodeKind("sample.loop")]
            public sealed partial class LoopNode : Node
            {
                public LoopNode(NodeGraph graph) : base(graph)
                {
                    DeclarePins();
                }

                public partial NodeInputExecPin {|NPA001:ExecutionPin|} { get; set; }
            }
            """,
            Id);
    }

    [Fact]
    public async Task APinTypeAttributeOnAPinThatTakesNoMemberIsReportedAtTheAttribute()
    {
        await PinAnalyzerHarness.AssertDiagnosticsAsync(
            PinSamples.Header + """
            [NodeKind("sample.loop")]
            public sealed partial class LoopNode : Node
            {
                public LoopNode(NodeGraph graph) : base(graph)
                {
                    DeclarePins();
                }

                [{|NPA001:PinType("Elsewhere")|}]
                public partial NodeInputExecPin ExecutionPin { get; }

                [{|NPA001:PinType("Elsewhere")|}]
                public partial NodeOutputDataPin<int> IndexPin { get; }
            }
            """,
            Id);
    }

    [Fact]
    public async Task APinAttributeOnAPropertyThatIsNoPinIsReportedAtTheAttribute()
    {
        await PinAnalyzerHarness.AssertDiagnosticsAsync(
            PinSamples.Header + """
            [NodeKind("sample.loop")]
            public sealed partial class LoopNode : Node
            {
                public LoopNode(NodeGraph graph) : base(graph)
                {
                    DeclarePins();
                }

                [{|NPA001:Pin("Count")|}]
                public int CountPin { get; }
            }
            """,
            Id);
    }

    [Fact]
    public async Task ClassesWithoutAMarkerAreLeftAlone()
    {
        await PinAnalyzerHarness.AssertDiagnosticsAsync(
            PinSamples.Header + """
            public sealed class Plain : Node
            {
                public Plain(NodeGraph graph) : base(graph)
                {
                }

                public NodeInputExecPin ExecutionPin => InputExecPins[0];
            }
            """,
            Id);
    }
}
