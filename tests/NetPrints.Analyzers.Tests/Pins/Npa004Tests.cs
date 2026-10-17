using System.Threading.Tasks;
using NetPrints.Analyzers.Tests.Infrastructure;
using Xunit;

namespace NetPrints.Analyzers.Tests.Pins;

/// <summary>NPA004: a pin key is empty or contains a dot, a slash or a tilde.</summary>
public class Npa004Tests
{
    private const string Id = "NPA004";

    [Fact]
    public async Task KeysWithReservedCharactersAreReportedAtTheKeyLiteral()
    {
        await PinAnalyzerHarness.AssertDiagnosticsAsync(
            PinSamples.Header + """
            [NodeKind("sample.keys")]
            public sealed partial class KeysNode : Node
            {
                public KeysNode(NodeGraph graph) : base(graph)
                {
                    DeclarePins();
                }

                [Pin({|NPA004:""|})]
                public partial NodeInputExecPin EmptyPin { get; }

                [Pin({|NPA004:"a.b"|})]
                public partial NodeInputExecPin DotPin { get; }

                [Pin({|NPA004:"a/b"|}, Role = PinRole.Result)]
                public partial NodeInputExecPin SlashPin { get; }

                [Pin({|NPA004:"a~b"|})]
                public partial NodeInputExecPin TildePin { get; }
            }
            """,
            Id);
    }

    [Fact]
    public async Task ValidKeysAndConventionKeysAreNotReported()
    {
        await PinAnalyzerHarness.AssertDiagnosticsAsync(
            PinSamples.Header + """
            [NodeKind("sample.keys")]
            public sealed partial class KeysNode : Node
            {
                public KeysNode(NodeGraph graph) : base(graph)
                {
                    DeclarePins();
                }

                [Pin("Then-1_x")]
                public partial NodeInputExecPin FirstPin { get; }

                public partial NodeOutputExecPin SecondPin { get; }
            }
            """,
            Id);
    }
}
