using Microsoft.Extensions.DependencyInjection;

namespace NetPrints.Extensibility.Testing.Checks;

/// <summary>NPK012: a freshly loaded extension is disposed, with a session still open, twice, and nothing it contributed fails to dispose.</summary>
internal sealed class DisposalCheck : ConformanceCheck
{
    private const string Rule = "everything the extension contributed disposes without failing, more than once";
    private const int Disposals = 2;

    public override string Code => NpkDiagnosticCodes.Disposal;

    public override string Name => "Disposal";

    public override async Task<CheckOutcome> RunAsync(CheckContext context, CancellationToken cancellationToken)
    {
        if (context.Options.HarnessFactory is not { } factory)
        {
            return CheckOutcome.Skip("no HarnessFactory");
        }

        ExtensionHarness harness = await factory(cancellationToken).ConfigureAwait(false);
        HarnessLog log = harness.Log;
        var findings = new List<ConformanceFinding>();
        using IServiceScope? session = context.SubjectId is { } id ? harness.Registry.OpenSession(id) : null;
        int before = log.Entries.Count;
        try
        {
            for (int attempt = 0; attempt < Disposals; attempt++)
            {
                await harness.DisposeAsync().ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            findings.Add(Finding(Rule, $"{ex.GetType().Name}: {ex.Message}"));
        }

        foreach (HarnessLogEntry entry in log.Entries.Skip(before))
        {
            findings.Add(Finding(Rule, entry.Message));
        }

        return CheckOutcome.FromFindings(findings);
    }
}
