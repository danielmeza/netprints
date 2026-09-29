namespace NetPrints.Extensibility.Loading;

/// <summary>
/// A contribution the registry rejected while its extension stayed loaded
/// (<see cref="ExtensionDiagnosticCodes.ContributionRejected"/>).
/// </summary>
/// <param name="ExtensionId">Id of the contributing extension.</param>
/// <param name="Code">The <c>NPX</c> code; always <see cref="ExtensionDiagnosticCodes.ContributionRejected"/>.</param>
/// <param name="Contribution">What was rejected, for example <c>node kind test/Log</c>.</param>
/// <param name="Reason">Why it was rejected.</param>
public sealed record ExtensionContributionIssue(string ExtensionId, string Code, string Contribution, string Reason);
