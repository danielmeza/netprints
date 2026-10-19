using NetPrints.Core;
using NetPrints.Extensibility.Nodes;

namespace NetPrints.Extensibility.Testing.Checks;

/// <summary>NPK008: translating the same graph twice gives identical text.</summary>
internal sealed class DeterministicEmittersCheck : ConformanceCheck
{
    private const string Rule = "two translations of the same graph are identical";

    public override string Code => NpkDiagnosticCodes.DeterministicEmitters;

    public override string Name => "Deterministic emitters";

    public override Task<CheckOutcome> RunAsync(CheckContext context, CancellationToken cancellationToken)
    {
        IReadOnlyList<NodeKindDescriptor> kinds = context.SubjectKinds;
        IReadOnlyList<NodeKindDescriptor> unexercised = context.UnexercisedKinds;
        if (kinds.Count == 0)
        {
            return Task.FromResult(unexercised.Count > 0
                ? CheckOutcome.Skip(CheckContext.NotExercised(unexercised))
                : CheckOutcome.Passed("the extension declares no node kind"));
        }

        var findings = new List<ConformanceFinding>();
        foreach (NodeKindDescriptor kind in kinds)
        {
            try
            {
                ClassGraph graph = context.Harness.BuildClass(ConformanceGraphs.Namespace, ConformanceGraphs.ClassName, kind.Kind);
                if (!string.Equals(context.Harness.Translate(graph), context.Harness.Translate(graph), StringComparison.Ordinal))
                {
                    findings.Add(Finding(Rule, $"{kind.Kind}: the generated text differs between runs"));
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                findings.Add(Finding(Rule, $"{kind.Kind}: translation threw {ex.GetType().Name}: {ex.Message}"));
            }
        }

        return Task.FromResult(findings.Count == 0 && unexercised.Count > 0
            ? CheckOutcome.Skip(CheckContext.NotExercised(unexercised))
            : CheckOutcome.FromFindings(findings));
    }
}
