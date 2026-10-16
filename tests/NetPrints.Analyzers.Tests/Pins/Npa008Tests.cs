using System.Threading.Tasks;
using NetPrints.Analyzers.Tests.Infrastructure;
using Xunit;

namespace NetPrints.Analyzers.Tests.Pins;

/// <summary>NPA008: a pin property name does not end in Pin and has no Pin attribute.</summary>
public class Npa008Tests
{
    private const string Id = "NPA008";

    [Fact]
    public async Task ANameWithoutTheSuffixAndWithoutAnAttributeIsReportedAtItsName()
    {
        await PinAnalyzerHarness.AssertDiagnosticsAsync(
            PinSamples.Header + """
            [NodeKind("sample.names")]
            public sealed partial class NamesNode : Node
            {
                public NamesNode(NodeGraph graph) : base(graph)
                {
                    DeclarePins();
                }

                public partial NodeInputExecPin {|NPA008:Execution|} { get; }

                public partial NodeOutputExecPin {|NPA008:Pin|} { get; }
            }
            """,
            Id);
    }

    [Fact]
    public async Task TheSuffixOrAPinAttributeSatisfiesTheRule()
    {
        await PinAnalyzerHarness.AssertDiagnosticsAsync(
            PinSamples.Header + """
            [NodeKind("sample.names")]
            public sealed partial class NamesNode : Node
            {
                public NamesNode(NodeGraph graph) : base(graph)
                {
                    DeclarePins();
                }

                public partial NodeInputExecPin ExecutionPin { get; }

                [Pin("Then")]
                public partial NodeOutputExecPin Next { get; }

                [Pin]
                public partial NodeOutputExecPin Else { get; }
            }
            """,
            Id);
    }
}
