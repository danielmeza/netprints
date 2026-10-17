using System.Threading.Tasks;
using NetPrints.Analyzers.Tests.Infrastructure;
using Xunit;

namespace NetPrints.Analyzers.Tests.Pins;

/// <summary>NPA006: a DefaultValue does not convert to the pin's T.</summary>
public class Npa006Tests
{
    private const string Id = "NPA006";

    [Fact]
    public async Task ADefaultThatDoesNotConvertIsReportedAtTheAttribute()
    {
        await PinAnalyzerHarness.AssertDiagnosticsAsync(
            PinSamples.Header + """
            [NodeKind("sample.defaults")]
            public sealed partial class DefaultsNode : Node
            {
                public DefaultsNode(NodeGraph graph) : base(graph)
                {
                    DeclarePins();
                }

                [{|NPA006:DefaultValue("three")|}]
                public partial NodeInputDataPin<int> CountPin { get; }

                [{|NPA006:DefaultValue(3)|}]
                public partial NodeInputDataPin<string> TextPin { get; }

                [{|NPA006:DefaultValue((string?)null)|}]
                public partial NodeInputDataPin<int> NullPin { get; }
            }
            """,
            Id);
    }

    [Fact]
    public async Task DefaultsThatConvertAreNotReported()
    {
        await PinAnalyzerHarness.AssertDiagnosticsAsync(
            PinSamples.Header + """
            [NodeKind("sample.defaults")]
            public sealed partial class DefaultsNode : Node
            {
                public DefaultsNode(NodeGraph graph) : base(graph)
                {
                    DeclarePins();
                }

                [DefaultValue(3)]
                public partial NodeInputDataPin<int> CountPin { get; }

                [DefaultValue(3)]
                public partial NodeInputDataPin<double> RatioPin { get; }

                [DefaultValue("hi")]
                public partial NodeInputDataPin<string> TextPin { get; }

                [DefaultValue((string?)null)]
                public partial NodeInputDataPin<string> NothingPin { get; }

                [DefaultValue(true)]
                public partial NodeInputDataPin<bool> FlagPin { get; }
            }
            """,
            Id);
    }
}
