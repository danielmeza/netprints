using NetPrints.Extensibility.Testing.Checks;

namespace NetPrints.Extensibility.Testing;

/// <summary>
/// Runs the conformance checks against an extension loaded into an <see cref="ExtensionHarness"/>.
/// </summary>
public static class ConformanceSuite
{
    private static readonly ConformanceCheck[] AllChecks =
    [
        new ManifestCheck(),
        new PackagingCheck(),
        new TypeIdentityCheck(),
        new RegistrationCheck(),
        new RegistryIssuesCheck(),
        new NodeRoundTripCheck(),
        new TranslationCompilesCheck(),
        new DeterministicEmittersCheck(),
        new SettingsCheck(),
        new HostChannelCheck(),
        new CoexistenceCheck(),
        new DisposalCheck(),
        new ServicesCheck(),
        new EditorContributionsCheck(),
        new ContributedViewsCheck(),
    ];

    /// <summary>Runs every check this version implements.</summary>
    /// <param name="harness">The harness the extension is loaded in.</param>
    /// <param name="options">What the checks need to know about the extension.</param>
    /// <param name="cancellationToken">Token to cancel the run.</param>
    /// <returns>One result per check, in code order.</returns>
    public static async Task<ConformanceReport> RunAsync(ExtensionHarness harness, ConformanceOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(harness);
        ArgumentNullException.ThrowIfNull(options);
        var context = new CheckContext(harness, options);
        var results = new List<ConformanceCheckResult>();
        foreach (ConformanceCheck check in AllChecks)
        {
            results.Add(await RunAsync(check, context, cancellationToken).ConfigureAwait(false));
        }

        return new ConformanceReport(results);
    }

    /// <summary>Runs one check.</summary>
    /// <param name="harness">The harness the extension is loaded in.</param>
    /// <param name="options">What the check needs to know about the extension.</param>
    /// <param name="code">The <c>NPK</c> code of the check.</param>
    /// <param name="cancellationToken">Token to cancel the run.</param>
    /// <returns>The result.</returns>
    /// <exception cref="ArgumentException"><paramref name="code"/> is not a check this version implements.</exception>
    public static Task<ConformanceCheckResult> RunCheckAsync(ExtensionHarness harness, ConformanceOptions options, string code, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(harness);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrEmpty(code);
        ConformanceCheck check = AllChecks.FirstOrDefault(candidate => string.Equals(candidate.Code, code, StringComparison.Ordinal))
            ?? throw new ArgumentException($"'{code}' is not a conformance check of this version.", nameof(code));
        return RunAsync(check, new CheckContext(harness, options), cancellationToken);
    }

    private static async Task<ConformanceCheckResult> RunAsync(ConformanceCheck check, CheckContext context, CancellationToken cancellationToken)
    {
        CheckOutcome outcome;
        try
        {
            outcome = await check.RunAsync(context, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            outcome = CheckOutcome.FromFindings([new ConformanceFinding(check.Code, "the check completes", $"{ex.GetType().Name}: {ex.Message}")]);
        }

        if (outcome.SkipReason is not null)
        {
            return new ConformanceCheckResult(check.Code, check.Name, ConformanceStatus.Skipped, [], outcome.SkipReason);
        }

        ConformanceStatus status = outcome.Findings.Count == 0 ? ConformanceStatus.Passed : ConformanceStatus.Failed;
        return new ConformanceCheckResult(check.Code, check.Name, status, outcome.Findings, outcome.Note);
    }
}
