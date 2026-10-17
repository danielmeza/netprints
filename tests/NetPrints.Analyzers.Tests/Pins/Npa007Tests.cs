using System.Threading.Tasks;
using NetPrints.Analyzers.Tests.Infrastructure;
using Xunit;

namespace NetPrints.Analyzers.Tests.Pins;

/// <summary>NPA007: the PinType member is missing or has the wrong type.</summary>
public class Npa007Tests
{
    private const string Id = "NPA007";

    [Fact]
    public async Task APinThatNeedsAMemberButHasNoPinTypeIsReportedAtItsName()
    {
        await PinAnalyzerHarness.AssertDiagnosticsAsync(
            PinSamples.Header + """
            [NodeKind("sample.untyped")]
            public sealed partial class UntypedNode : Node
            {
                public UntypedNode(NodeGraph graph) : base(graph)
                {
                    DeclarePins();
                }

                public partial NodeInputDataPin {|NPA007:ValuePin|} { get; }

                public partial NodeOutputDataPin {|NPA007:ResultPin|} { get; }

                public partial NodeOutputTypePin {|NPA007:ResultTypePin|} { get; }
            }
            """,
            Id);
    }

    [Fact]
    public async Task AMemberThatDoesNotExistOrHasTheWrongTypeIsReportedAtTheAttribute()
    {
        await PinAnalyzerHarness.AssertDiagnosticsAsync(
            PinSamples.Header + """
            [NodeKind("sample.untyped")]
            public sealed partial class UntypedNode : Node
            {
                private readonly int wrong = 0;

                public UntypedNode(NodeGraph graph) : base(graph)
                {
                    DeclarePins();
                }

                [{|NPA007:PinType("missing")|}]
                public partial NodeOutputDataPin ResultPin { get; }

                [{|NPA007:PinType("wrong")|}]
                public partial NodeOutputTypePin TypePin { get; }
            }
            """,
            Id);
    }

    [Fact]
    public async Task AMemberOfTheRightTypeIsNotReported()
    {
        await PinAnalyzerHarness.AssertDiagnosticsAsync(
            PinSamples.Header + """
            [NodeKind("sample.untyped")]
            public sealed partial class UntypedNode : Node
            {
                private readonly ObservableValue<BaseType> resultType = new(TypeSpecifier.FromType<int>());

                public UntypedNode(NodeGraph graph) : base(graph)
                {
                    DeclarePins();
                }

                protected ObservableValue<BaseType> OtherType { get; } = new(TypeSpecifier.FromType<int>());

                [PinType(nameof(resultType))]
                public partial NodeOutputDataPin ResultPin { get; }

                [PinType("OtherType")]
                public partial NodeInputDataPin ValuePin { get; }

                [PinType("resultType")]
                public partial NodeOutputTypePin TypePin { get; }
            }
            """,
            Id);
    }
}
