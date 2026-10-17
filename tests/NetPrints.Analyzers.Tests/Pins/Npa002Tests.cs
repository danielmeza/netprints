using System.Threading.Tasks;
using NetPrints.Analyzers.Tests.Infrastructure;
using Xunit;

namespace NetPrints.Analyzers.Tests.Pins;

/// <summary>NPA002: duplicate pin key in one collection, including the keys of base classes.</summary>
public class Npa002Tests
{
    private const string Id = "NPA002";

    [Fact]
    public async Task TwoPinsWithTheSameKeyInOneCollectionReportTheSecond()
    {
        await PinAnalyzerHarness.AssertDiagnosticsAsync(
            PinSamples.Header + """
            [NodeKind("sample.dup")]
            public sealed partial class DupNode : Node
            {
                public DupNode(NodeGraph graph) : base(graph)
                {
                    DeclarePins();
                }

                public partial NodeInputDataPin<int> ValuePin { get; }

                [Pin("Value")]
                public partial NodeInputDataPin<int> {|NPA002:OtherPin|} { get; }
            }
            """,
            Id);
    }

    [Fact]
    public async Task TheSameKeyInDifferentCollectionsIsFine()
    {
        await PinAnalyzerHarness.AssertDiagnosticsAsync(
            PinSamples.Header + """
            [NodeKind("sample.same")]
            public sealed partial class SameNode : Node
            {
                public SameNode(NodeGraph graph) : base(graph)
                {
                    DeclarePins();
                }

                public partial NodeInputDataPin<int> ValuePin { get; }

                public partial NodeOutputDataPin<int> ValueOutPin { get; }

                [Pin("Value")]
                public partial NodeOutputDataPin<int> ResultPin { get; }
            }
            """,
            Id);
    }

    [Fact]
    public async Task AKeyThatABaseClassDeclaresIsReportedOnTheDerivedClass()
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
                public DerivedNode(NodeGraph graph) : base(graph)
                {
                    DeclarePins();
                }

                public partial NodeInputExecPin {|NPA002:ExecutionPin|} { get; }
            }
            """,
            Id);
    }
}
