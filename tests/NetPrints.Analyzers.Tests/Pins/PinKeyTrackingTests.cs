using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using NetPrints.Analyzers.Tests.Infrastructure;
using Xunit;

namespace NetPrints.Analyzers.Tests.Pins;

/// <summary>NPA010 and NPA011: a declared pin key is recorded, and a shipped pin key never disappears (FR-011).</summary>
public class PinKeyTrackingTests
{
    private const string Kind = "sample.loop";

    private const string ShippedLoop = "sample.loop in.exec.Execution\nsample.loop out.exec.Loop\n";

    private const string ExecutionLine = "sample.loop in.exec.Execution\n";

    private const string LoopLine = "sample.loop out.exec.Loop";

    private static string Loop(string members) =>
        PinSamples.Header + $$"""
            [NodeKind("{{Kind}}")]
            public sealed partial class {|NPA011:LoopNode|} : Node
            {
                public LoopNode(NodeGraph graph) : base(graph)
                {
                    DeclarePins();
                }

            {{members}}
            }
            """;

    private static string Plain(string members) => Loop(members).Replace("{|NPA011:LoopNode|}", "LoopNode", System.StringComparison.Ordinal);

    private const string Both = """
            public partial NodeInputExecPin ExecutionPin { get; }

            public partial NodeOutputExecPin LoopPin { get; }
        """;

    [Fact]
    public async Task NPA010ReportsADeclaredKeyThatIsNotRecorded()
    {
        const string members = """
                public partial NodeInputExecPin ExecutionPin { get; }

                public partial NodeOutputExecPin {|NPA010:LoopPin|} { get; }
            """;

        await PinAnalyzerHarness.AssertTrackedDiagnosticsAsync(Plain(members), new PinAnalyzerHarness.TrackingFiles(ExecutionLine, string.Empty), "NPA010", "NPA011");
    }

    [Fact]
    public async Task KeysRecordedInEitherFileAreAccepted()
    {
        await PinAnalyzerHarness.AssertTrackedDiagnosticsAsync(Plain(Both), new PinAnalyzerHarness.TrackingFiles(ExecutionLine, LoopLine + "\n"), "NPA010", "NPA011");
    }

    [Fact]
    public async Task WithoutTheFilesNothingIsTracked()
    {
        await PinAnalyzerHarness.AssertDiagnosticsAsync(Plain(Both), "NPA010", "NPA011");
    }

    [Fact]
    public async Task NPA011NamesTheOldAndTheNewKeyWhenAShippedKeyChanges()
    {
        const string members = """
                public partial NodeInputExecPin ExecutionPin { get; }

                public partial NodeOutputExecPin {|NPA011:RepeatPin|} { get; }
            """;

        ImmutableArray<Diagnostic> diagnostics = await PinAnalyzerHarness.AssertTrackedDiagnosticsAsync(
            Plain(members),
            new PinAnalyzerHarness.TrackingFiles(ShippedLoop, string.Empty),
            "NPA010",
            "NPA011");

        string message = diagnostics[0].GetMessage(System.Globalization.CultureInfo.InvariantCulture);
        Assert.Contains("out.exec.Loop", message, System.StringComparison.Ordinal);
        Assert.Contains("out.exec.Repeat", message, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task NPA011ReportsAShippedKeyWhoseSlotIsGone()
    {
        const string members = """
                public partial NodeInputExecPin ExecutionPin { get; }
            """;

        ImmutableArray<Diagnostic> diagnostics = await PinAnalyzerHarness.AssertTrackedDiagnosticsAsync(
            Loop(members),
            new PinAnalyzerHarness.TrackingFiles(ShippedLoop, string.Empty),
            "NPA011");

        Assert.Contains("out.exec.Loop", diagnostics[0].GetMessage(System.Globalization.CultureInfo.InvariantCulture), System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARemovedLineAllowsTheKeyToGo()
    {
        const string members = """
                public partial NodeInputExecPin ExecutionPin { get; }
            """;

        await PinAnalyzerHarness.AssertTrackedDiagnosticsAsync(
            Plain(members),
            new PinAnalyzerHarness.TrackingFiles(ShippedLoop, "*REMOVED*" + LoopLine + "\n"),
            "NPA010",
            "NPA011");
    }

    [Fact]
    public async Task NPA010FixRecordsTheKeyInTheUnshippedFile()
    {
        const string members = """
                public partial NodeInputExecPin ExecutionPin { get; }

                public partial NodeOutputExecPin LoopPin { get; }
            """;

        PinAnalyzerHarness.TrackedFix fix = await PinAnalyzerHarness.FixTrackedAsync(
            Plain(members),
            new PinAnalyzerHarness.TrackingFiles(ExecutionLine, string.Empty),
            "NPA010",
            "Record the pin key in NodePins.Unshipped.txt");

        Assert.Equal(LoopLine + "\n", fix.Unshipped);
        Assert.Equal(Plain(members), fix.Source);
    }

    [Fact]
    public async Task NPA011KeepKeyFixPinsTheOldKeyOnTheRenamedProperty()
    {
        const string members = """
                public partial NodeInputExecPin ExecutionPin { get; }

                public partial NodeOutputExecPin RepeatPin { get; }
            """;

        PinAnalyzerHarness.TrackedFix fix = await PinAnalyzerHarness.FixTrackedAsync(
            Plain(members),
            new PinAnalyzerHarness.TrackingFiles(ShippedLoop, string.Empty),
            "NPA011",
            "Keep the shipped pin key");

        Assert.Equal(
            Plain("""
                public partial NodeInputExecPin ExecutionPin { get; }

                [Pin("Loop")]
                public partial NodeOutputExecPin RepeatPin { get; }
            """),
            fix.Source);
        Assert.Equal(string.Empty, fix.Unshipped);
    }

    [Fact]
    public async Task NPA011RemoveOnPurposeFixRecordsARemovedLine()
    {
        const string members = """
                public partial NodeInputExecPin ExecutionPin { get; }
            """;

        PinAnalyzerHarness.TrackedFix fix = await PinAnalyzerHarness.FixTrackedAsync(
            Plain(members),
            new PinAnalyzerHarness.TrackingFiles(ShippedLoop, string.Empty),
            "NPA011",
            "Remove the shipped pin key on purpose");

        Assert.Equal("*REMOVED*" + LoopLine + "\n", fix.Unshipped);
    }

    [Fact]
    public async Task NPA011IgnoresShippedKindsTheCompilationDoesNotDeclare()
    {
        await PinAnalyzerHarness.AssertTrackedDiagnosticsAsync(
            Plain(Both),
            new PinAnalyzerHarness.TrackingFiles(ShippedLoop + "sample.other in.exec.Exec\n", string.Empty),
            "NPA010",
            "NPA011");
    }
}
