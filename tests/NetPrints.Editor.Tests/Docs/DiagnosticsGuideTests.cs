using System.Text.RegularExpressions;
using NetPrints.Editor.Tests.Architecture;

namespace NetPrints.Editor.Tests.Docs;

/// <summary>
/// <c>docs/guide/diagnostics.md</c> lists every diagnostic prefix and code with its severity and cause (FR-008),
/// kept in step with the <c>*DiagnosticCodes</c> classes and the analyzer release files (analyzer-diagnostics.md §1).
/// </summary>
public sealed partial class DiagnosticsGuideTests
{
    private static readonly string[] Prefixes = ["NPC", "NPD", "NPE", "NPI", "NPS", "NPT", "NPW", "NPX", "NPXE"];

    private static readonly string[] Severities = ["error", "warning", "info", "suggestion"];

    private static string Root => RepositoryPaths.Root();

    private static string GuidePath => Path.Combine(Root, "docs", "guide", "diagnostics.md");

    [GeneratedRegex(@"^\|\s*(?<code>NP[A-Z]{1,2}\d{3,4})\s*\|\s*(?<severity>[^|]+?)\s*\|\s*(?<cause>[^|]+?)\s*\|\s*$", RegexOptions.Multiline)]
    private static partial Regex GuideRow();

    [GeneratedRegex(@"const\s+string\s+\w+\s*=\s*""(?<code>NP[A-Z]{1,2}\d{3,4})""")]
    private static partial Regex CodeConstant();

    [GeneratedRegex(@"^(?<code>NP[A-Z]{1,2}\d{3,4})\s*\|", RegexOptions.Multiline)]
    private static partial Regex ReleaseRule();

    private static IEnumerable<string> SourceFiles(string pattern) =>
        Directory.EnumerateFiles(Path.Combine(Root, "src"), pattern, SearchOption.AllDirectories)
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && !path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal));

    private static List<(string Code, string Severity, string Cause)> Rows() =>
        [.. GuideRow().Matches(File.ReadAllText(GuidePath)).Select(match => (match.Groups["code"].Value, match.Groups["severity"].Value, match.Groups["cause"].Value))];

    private static string[] Declared() =>
    [
        .. SourceFiles("*DiagnosticCodes.cs").SelectMany(path => CodeConstant().Matches(File.ReadAllText(path)).Select(match => match.Groups["code"].Value))
            .Concat(SourceFiles("AnalyzerReleases.*.md").SelectMany(path => ReleaseRule().Matches(File.ReadAllText(path)).Select(match => match.Groups["code"].Value)))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal),
    ];

    [Fact]
    public void TheGuideExists()
    {
        Assert.True(File.Exists(GuidePath), "docs/guide/diagnostics.md does not exist.");
    }

    [Fact]
    public void EveryPrefixHasASection()
    {
        string guide = File.ReadAllText(GuidePath);

        foreach (string prefix in Prefixes)
        {
            Assert.True(Regex.IsMatch(guide, $@"^##\s+{prefix}\b", RegexOptions.Multiline), $"The guide has no section for the {prefix} prefix.");
        }
    }

    [Fact]
    public void EveryDeclaredCodeIsListed()
    {
        string[] listed = [.. Rows().Select(row => row.Code)];

        string[] missing = [.. Declared().Except(listed, StringComparer.Ordinal)];

        Assert.True(missing.Length == 0, $"docs/guide/diagnostics.md misses: {string.Join(", ", missing)}");
    }

    [Fact]
    public void EveryRowHasASeverityAndACause()
    {
        foreach ((string code, string severity, string cause) in Rows())
        {
            Assert.True(Severities.Any(known => severity.StartsWith(known, StringComparison.OrdinalIgnoreCase)), $"{code} has the severity '{severity}'.");
            Assert.False(string.IsNullOrWhiteSpace(cause), $"{code} has no cause.");
        }
    }

    [Fact]
    public void EveryListedCodeExistsInTheSources()
    {
        string sources = string.Concat(SourceFiles("*.cs").Select(File.ReadAllText));
        string releases = string.Concat(SourceFiles("AnalyzerReleases.*.md").Select(File.ReadAllText));

        string[] unknown = [.. Rows().Select(row => row.Code).Where(code => !sources.Contains($"\"{code}\"", StringComparison.Ordinal) && !releases.Contains(code, StringComparison.Ordinal))];

        Assert.True(unknown.Length == 0, $"docs/guide/diagnostics.md lists codes no source declares: {string.Join(", ", unknown)}");
    }

    [Fact]
    public void EveryRowIsListedOnce()
    {
        string[] codes = [.. Rows().Select(row => row.Code)];

        Assert.Equal(codes.Length, codes.Distinct(StringComparer.Ordinal).Count());
    }
}
