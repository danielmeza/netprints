using System.Threading.Tasks;
using NetPrints.Analyzers.Tests.Infrastructure;
using Xunit;

namespace NetPrints.Analyzers.Tests.Pins;

/// <summary>NPA009: pin properties in a class without NodeKind or NodeBase.</summary>
public class Npa009Tests
{
    private const string Id = "NPA009";

    [Fact]
    public async Task PartialPinPropertiesInAnUnmarkedClassAreReportedAtTheirNames()
    {
        await PinAnalyzerHarness.AssertDiagnosticsAsync(
            PinSamples.Header + """
            public sealed partial class LoopNode : Node
            {
                public LoopNode(NodeGraph graph) : base(graph)
                {
                }

                public partial NodeInputExecPin {|NPA009:ExecutionPin|} { get; }

                [Pin("Out")]
                public partial NodeOutputDataPin<int> {|NPA009:IndexPin|} { get; }
            }
            """,
            Id);
    }

    [Fact]
    public async Task PartialPropertiesThatTheMvvmToolkitImplementsAreNotPinProperties()
    {
        await PinAnalyzerHarness.AssertDiagnosticsAsync(
            PinSamples.Header + """
            public sealed partial class Observing : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
            {
                [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
                public partial NodeInputExecPin IncomingPin { get; set; }
            }
            """,
            Id);
    }

    [Fact]
    public async Task MarkedClassesAndOrdinaryPinPropertiesAreNotReported()
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
            }

            [NodeBase]
            public abstract partial class ExecBase : Node
            {
                protected ExecBase(NodeGraph graph) : base(graph)
                {
                }

                public partial NodeInputExecPin ExecutionPin { get; }
            }

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
