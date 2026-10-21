using System.Threading.Tasks;
using NetPrints.Analyzers.Tests.Infrastructure;
using Xunit;

namespace NetPrints.Analyzers.Tests.Pins;

/// <summary>NPA003: the constructor does not call DeclarePins() exactly once, or adds pins before it.</summary>
public class Npa003Tests
{
    private const string Id = "NPA003";

    [Fact]
    public async Task AConstructorThatCallsDeclarePinsOnceIsNotReported()
    {
        await PinAnalyzerHarness.AssertDiagnosticsAsync(
            PinSamples.Header + """
            [NodeKind("sample.loop")]
            public sealed partial class LoopNode : Node
            {
                public LoopNode(NodeGraph graph) : base(graph)
                {
                    DeclarePins();
                    AddInputExecPin("Late");
                }

                public partial NodeInputExecPin ExecutionPin { get; }
            }
            """,
            Id);
    }

    [Fact]
    public async Task AConstructorThatNeverCallsDeclarePinsIsReportedAtItsName()
    {
        await PinAnalyzerHarness.AssertDiagnosticsAsync(
            PinSamples.Header + """
            [NodeKind("sample.loop")]
            public sealed partial class LoopNode : Node
            {
                public {|NPA003:LoopNode|}(NodeGraph graph) : base(graph)
                {
                }

                public partial NodeInputExecPin ExecutionPin { get; }
            }
            """,
            Id);
    }

    [Fact]
    public async Task AConstructorThatCallsDeclarePinsTwiceIsReportedAtItsName()
    {
        await PinAnalyzerHarness.AssertDiagnosticsAsync(
            PinSamples.Header + """
            [NodeKind("sample.loop")]
            public sealed partial class LoopNode : Node
            {
                public {|NPA003:LoopNode|}(NodeGraph graph) : base(graph)
                {
                    DeclarePins();
                    this.DeclarePins();
                }

                public partial NodeInputExecPin ExecutionPin { get; }
            }
            """,
            Id);
    }

    [Fact]
    public async Task AClassWithoutAConstructorIsReportedAtItsName()
    {
        await PinAnalyzerHarness.AssertDiagnosticsAsync(
            PinSamples.Header + """
            [NodeKind("sample.loop")]
            public sealed partial class {|NPA003:LoopNode|} : Node
            {
                public partial NodeInputExecPin ExecutionPin { get; }
            }
            """,
            Id);
    }

    [Fact]
    public async Task APinAddedBeforeDeclarePinsIsReportedAtTheCall()
    {
        await PinAnalyzerHarness.AssertDiagnosticsAsync(
            PinSamples.Header + """
            [NodeKind("sample.loop")]
            public sealed partial class LoopNode : Node
            {
                public LoopNode(NodeGraph graph) : base(graph)
                {
                    {|NPA003:AddInputExecPin("Early")|};
                    {|NPA003:InputExecPins.Add(new NodeInputExecPin(this, "Other"))|};
                    OutputDataPins.Clear();
                    DeclarePins();
                }

                public partial NodeInputExecPin ExecutionPin { get; }
            }
            """,
            Id);
    }

    [Fact]
    public async Task ADataPinAddedBeforeDeclarePinsIsReportedAtTheCall()
    {
        await PinAnalyzerHarness.AssertDiagnosticsAsync(
            PinSamples.Header + """
            [NodeKind("sample.value")]
            public sealed partial class ValueNode : Node
            {
                public ValueNode(NodeGraph graph) : base(graph)
                {
                    {|NPA003:AddInputDataPin("Early", new ObservableValue<BaseType>(TypeSpecifier.FromType<int>()))|};
                    AddOutputDataPin("Other", new ObservableValue<BaseType>(TypeSpecifier.FromType<int>()));
                    DeclarePins();
                }

                public partial NodeInputDataPin<int> ValuePin { get; }
            }
            """,
            Id);
    }

    [Fact]
    public async Task AKindWhoseOnlyPinsComeFromABaseIsCheckedToo()
    {
        await PinAnalyzerHarness.AssertDiagnosticsAsync(
            PinSamples.Header + """
            [NodeBase]
            public abstract partial class ExecBase : Node
            {
                protected ExecBase(NodeGraph graph) : base(graph)
                {
                }

                public partial NodeInputExecPin ExecutionPin { get; }
            }

            [NodeKind("sample.derived")]
            public sealed partial class DerivedNode : ExecBase
            {
                public {|NPA003:DerivedNode|}(NodeGraph graph) : base(graph)
                {
                }
            }

            [NodeKind("sample.good")]
            public sealed partial class GoodNode : ExecBase
            {
                public GoodNode(NodeGraph graph) : base(graph)
                {
                    DeclarePins();
                }
            }
            """,
            Id);
    }

    [Fact]
    public async Task AConstructorThatChainsToAnotherOneIsNotReported()
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

                public LoopNode(NodeGraph graph, int extra) : this(graph)
                {
                }

                public partial NodeInputExecPin ExecutionPin { get; }
            }
            """,
            Id);
    }

    [Fact]
    public async Task ABaseClassMarkerAndAClassWithoutPinsAreLeftAlone()
    {
        await PinAnalyzerHarness.AssertDiagnosticsAsync(
            PinSamples.Header + """
            [NodeBase]
            public abstract partial class ExecBase : Node
            {
                protected ExecBase(NodeGraph graph) : base(graph)
                {
                }

                public partial NodeInputExecPin ExecutionPin { get; }
            }

            [NodeKind("sample.empty")]
            public sealed partial class EmptyNode : Node
            {
                public EmptyNode(NodeGraph graph) : base(graph)
                {
                }
            }
            """,
            Id);
    }
}
