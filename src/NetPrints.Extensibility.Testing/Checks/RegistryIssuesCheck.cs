using NetPrints.Extensibility.Loading;

namespace NetPrints.Extensibility.Testing.Checks;

/// <summary>NPK005: the extension loads and its contributions raise no registry issue.</summary>
internal sealed class RegistryIssuesCheck : ConformanceCheck
{
    private const string Rule = "the extension loads and contributes without registry issues";

    public override string Code => NpkDiagnosticCodes.RegistryIssues;

    public override string Name => "Registry issues";

    public override Task<CheckOutcome> RunAsync(CheckContext context, CancellationToken cancellationToken)
    {
        if (context.SubjectId is not { } id)
        {
            return Task.FromResult(CheckOutcome.Skip("no extension besides the built-in one was loaded"));
        }

        ExtensionRegistry registry = context.Harness.Registry;
        var findings = new List<ConformanceFinding>();
        foreach (ExtensionLoadResult.Failed failure in registry.Results.OfType<ExtensionLoadResult.Failed>().Where(result => result.Id == id && !ServicesCheck.IsBuildFailure(result)))
        {
            findings.Add(Finding(Rule, $"{failure.Code} {failure.Reason}"));
        }

        foreach (ExtensionContributionIssue issue in registry.Issues.Where(issue => issue.ExtensionId == id && !ServicesCheck.IsRejectedService(issue)))
        {
            findings.Add(Finding(Rule, $"{issue.Code} {issue.Contribution}: {issue.Reason}"));
        }

        return Task.FromResult(CheckOutcome.FromFindings(findings));
    }
}
