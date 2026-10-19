using NetPrints.Extensibility.Loading;

namespace NetPrints.Extensibility.Testing.Checks;

/// <summary>NPK004: registration is pure and repeatable; loading the extension again gives the same registry.</summary>
internal sealed class RegistrationCheck : ConformanceCheck
{
    private const string Rule = "registering twice gives the same registry";
    private const int MaxListed = 3;

    public override string Code => NpkDiagnosticCodes.Registration;

    public override string Name => "Registration";

    public override async Task<CheckOutcome> RunAsync(CheckContext context, CancellationToken cancellationToken)
    {
        if (context.Options.HarnessFactory is not { } factory)
        {
            return CheckOutcome.Skip("no HarnessFactory");
        }

        ExtensionHarness second = await factory(cancellationToken).ConfigureAwait(false);
        await using (second.ConfigureAwait(false))
        {
            List<string> first = Fingerprint(context.Harness.Registry);
            List<string> again = Fingerprint(second.Registry);
            string[] onlyFirst = [.. first.Except(again, StringComparer.Ordinal)];
            string[] onlyAgain = [.. again.Except(first, StringComparer.Ordinal)];
            if (onlyFirst.Length == 0 && onlyAgain.Length == 0)
            {
                return first.SequenceEqual(again, StringComparer.Ordinal)
                    ? CheckOutcome.Pass
                    : CheckOutcome.FromFindings([Finding(Rule, "the contributions are registered in a different order")]);
            }

            return CheckOutcome.FromFindings([Finding(Rule, $"first load only: [{string.Join(", ", onlyFirst.Take(MaxListed))}]; second load only: [{string.Join(", ", onlyAgain.Take(MaxListed))}]")]);
        }
    }

    private static List<string> Fingerprint(ExtensionRegistry registry)
    {
        var lines = new List<string>();
        lines.AddRange(registry.Loaded.Select(manifest => $"extension {manifest.Id}"));
        lines.AddRange(registry.NodeKinds.Select(kind => $"node kind {kind.Kind} ({kind.NodeType.FullName})"));
        lines.AddRange(registry.ClassEmitters.Select(emitter => $"class emitter {emitter.Id}"));
        lines.AddRange(registry.MemberEmitters.Select(emitter => $"member emitter {emitter.Id}"));
        lines.AddRange(registry.Profiles.Select(profile => $"project profile {profile.Id}"));
        lines.AddRange(registry.HostChannels.Select(factory => $"host channel {factory.Id}"));
        lines.AddRange(registry.Settings.Select(settings => $"settings {settings.ExtensionId}"));
        lines.AddRange(registry.CatalogProfiles.Select(profile => $"catalog profile {profile.Id}"));
        lines.AddRange(registry.SuggestionProviders.Select(provider => $"suggestion provider {provider.Id}"));
        lines.AddRange(registry.ProjectTemplates.Select(template => $"project template {template.Id}"));
        lines.AddRange(registry.ProjectProperties.Select(property => $"project property {property}"));
        lines.AddRange(registry.Issues.Select(issue => $"issue {issue.Code} {issue.Contribution}"));
        return lines;
    }
}
