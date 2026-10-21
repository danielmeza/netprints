using NetPrints.Core;
using NetPrints.Extensibility.Nodes;

namespace NetPrints.Extensibility.Testing.Checks;

/// <summary>NPK006: a graph with a node of each kind of the extension is written, read and written again to the same bytes.</summary>
internal sealed class NodeRoundTripCheck : ConformanceCheck
{
    private const string Rule = "a node kind is saved and loaded unchanged";

    public override string Code => NpkDiagnosticCodes.NodeRoundTrip;

    public override string Name => "Node round trip";

    public override async Task<CheckOutcome> RunAsync(CheckContext context, CancellationToken cancellationToken)
    {
        IReadOnlyList<NodeKindDescriptor> kinds = context.SubjectKinds;
        IReadOnlyList<NodeKindDescriptor> unexercised = context.UnexercisedKinds;
        if (kinds.Count == 0)
        {
            return unexercised.Count > 0
                ? CheckOutcome.Skip(CheckContext.NotExercised(unexercised))
                : CheckOutcome.Passed("the extension declares no node kind");
        }

        var findings = new List<ConformanceFinding>();
        foreach (NodeKindDescriptor kind in kinds)
        {
            try
            {
                ClassGraph graph = context.Harness.BuildClass(ConformanceGraphs.Namespace, ConformanceGraphs.ClassName, kind.Kind);
                byte[] written = await context.Harness.WriteAsync(graph, cancellationToken).ConfigureAwait(false);
                byte[] again = await context.Harness.RoundTripAsync(written, cancellationToken).ConfigureAwait(false);
                if (!written.AsSpan().SequenceEqual(again))
                {
                    findings.Add(Finding(Rule, $"{kind.Kind}: the document differs after it is read and written again"));
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                findings.Add(Finding(Rule, $"{kind.Kind}: {ex.GetType().Name}: {ex.Message}"));
            }
        }

        return findings.Count == 0 && unexercised.Count > 0
            ? CheckOutcome.Skip(CheckContext.NotExercised(unexercised))
            : CheckOutcome.FromFindings(findings);
    }
}
