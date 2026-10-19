using System.Collections;
using NetPrints.Extensibility.Loading;

namespace NetPrints.Extensibility.Testing.Checks;

/// <summary>NPK011: loading the extension next to the built-in extension and a neighbour that fails and grabs its names changes nothing for either side.</summary>
internal sealed class CoexistenceCheck : ConformanceCheck
{
    private const string Rule = "a load changes nothing outside the extension and nothing a neighbour does changes the extension";
    private const string NeighbourId = "harness.noisy";
    private const string GreedyId = "harness.greedy";
    private const string ManifestVersion = "1.0.0";

    public override string Code => NpkDiagnosticCodes.Coexistence;

    public override string Name => "Coexistence";

    public override async Task<CheckOutcome> RunAsync(CheckContext context, CancellationToken cancellationToken)
    {
        if (context.Options.NeighbourHarnessFactory is not { } factory)
        {
            return CheckOutcome.Skip("no NeighbourHarnessFactory");
        }

        if (context.SubjectId is not { } id)
        {
            return CheckOutcome.Skip("no extension besides the built-in one was loaded");
        }

        string api = $"{ExtensionApi.Version.Major}.{ExtensionApi.Version.Minor}";
        List<(ExtensionManifest, INetPrintsExtension)> neighbours =
        [
            (new ExtensionManifest(NeighbourId, NeighbourId, ManifestVersion, NeighbourId + ".dll", api, []), new ThrowingNeighbour()),
            (new ExtensionManifest(GreedyId, GreedyId, ManifestVersion, GreedyId + ".dll", api, []), new GreedyNeighbour(context.Harness.Registry.ProjectProperties)),
        ];

        string directory = Directory.GetCurrentDirectory();
        Dictionary<string, string> environment = Environment();
        ExtensionHarness crowded = await factory(neighbours, cancellationToken).ConfigureAwait(false);
        await using (crowded.ConfigureAwait(false))
        {
            var findings = new List<ConformanceFinding>();
            Dictionary<string, string> after = Environment();
            foreach (string name in environment.Keys.Union(after.Keys).Order(StringComparer.Ordinal))
            {
                if (environment.GetValueOrDefault(name) != after.GetValueOrDefault(name))
                {
                    findings.Add(Finding(Rule, $"loading the extension changed the environment variable {name}"));
                }
            }

            if (!string.Equals(directory, Directory.GetCurrentDirectory(), StringComparison.Ordinal))
            {
                findings.Add(Finding(Rule, "loading the extension changed the current directory"));
            }

            List<string> alone = View(context.Harness.Registry, id);
            List<string> together = View(crowded.Registry, id);
            foreach (string line in alone.Except(together, StringComparer.Ordinal))
            {
                findings.Add(Finding(Rule, $"{line} is missing when a neighbour is loaded"));
            }

            foreach (string line in together.Except(alone, StringComparer.Ordinal))
            {
                findings.Add(Finding(Rule, $"{line} appears only when a neighbour is loaded"));
            }

            return CheckOutcome.FromFindings(findings);
        }
    }

    private static Dictionary<string, string> Environment() =>
        System.Environment.GetEnvironmentVariables().Cast<DictionaryEntry>().ToDictionary(entry => (string)entry.Key, entry => entry.Value?.ToString() ?? string.Empty, StringComparer.Ordinal);

    private static List<string> View(ExtensionRegistry registry, string id)
    {
        var lines = new List<string>();
        lines.AddRange(registry.Results.Where(result => result.Id == id).Select(result => $"load result {result.GetType().Name}"));
        lines.AddRange(registry.NodeKinds.Where(kind => kind.Kind.StartsWith(id + "/", StringComparison.Ordinal)).Select(kind => $"node kind {kind.Kind}"));
        lines.AddRange(registry.Settings.Where(settings => settings.ExtensionId == id).Select(settings => $"settings {settings.ExtensionId}"));
        lines.AddRange(registry.Issues.Where(issue => issue.ExtensionId == id).Select(issue => $"issue {issue.Code} {issue.Contribution}"));
        return lines;
    }

    private sealed class ThrowingNeighbour : INetPrintsExtension
    {
        public void Register(IExtensionBuilder builder) => throw new InvalidOperationException("The neighbour fails on purpose.");
    }

    private sealed class GreedyNeighbour(IReadOnlyList<string> properties) : INetPrintsExtension
    {
        public void Register(IExtensionBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);
            foreach (string property in properties)
            {
                builder.AddProjectProperty(property);
            }
        }
    }
}
