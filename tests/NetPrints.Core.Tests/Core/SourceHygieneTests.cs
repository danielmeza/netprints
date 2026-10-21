using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NetPrints.Tests.Samples;
using Xunit;

namespace NetPrints.Tests.Core
{
    /// <summary>FR-058: the product never resolves references from a hard-coded Windows install path.</summary>
    public partial class SourceHygieneTests
    {
        private static string[] EnumerateSourceFiles(string root, string pattern) =>
            [.. Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories)
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))];

        /// <summary>
        /// Every <c>src/**/*.cs</c> and <c>tests/**/*.cs</c> file under <paramref name="repoRoot"/> (R1-22/R2-18/R3-15:
        /// the "no <c>!</c>, no unlisted suppression" gates cover <c>tests/</c> too, not just <c>src/</c>).
        /// </summary>
        private static string[] EnumerateSourceAndTestFiles(string repoRoot) =>
            [.. new[] { "src", "tests" }.SelectMany(dir => EnumerateSourceFiles(Path.Combine(repoRoot, dir), "*.cs"))];

        [Fact]
        public void NoSourceFileMentionsProgramFilesX86()
        {
            string src = Path.Combine(SampleProjectFactory.FindRepositoryRoot(), "src");
            string[] offenders = Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .Where(path => File.ReadAllText(path).Contains("ProgramFilesX86", StringComparison.Ordinal))
                .Select(path => Path.GetRelativePath(src, path))
                .ToArray();

            Assert.Empty(offenders);
        }

        /// <summary>
        /// A stable diagnostic code is declared once across <c>src/</c>. NPH003 covers the literals and the
        /// duplicates inside one project; this covers a code declared in two projects, of which only
        /// <c>NPXE0004</c> is allowed.
        /// </summary>
        [Fact]
        public void NoDiagnosticCodeIsDeclaredInTwoProjects()
        {
            string src = Path.Combine(SampleProjectFactory.FindRepositoryRoot(), "src");
            Regex declarationPattern = DiagnosticCodeDeclarationPattern();

            var declaredCodes = new List<(string Code, string File)>();
            foreach (string path in EnumerateSourceFiles(src, "*.cs"))
            {
                string relativePath = Path.GetRelativePath(src, path);
                foreach (Match declaration in declarationPattern.Matches(File.ReadAllText(path)))
                {
                    declaredCodes.Add((declaration.Groups["code"].Value, relativePath));
                }
            }

            Assert.NotEmpty(declaredCodes);

            IEnumerable<string> duplicates = declaredCodes.GroupBy(declaration => declaration.Code, StringComparer.Ordinal)
                .Where(group => group.Count() > 1 && !IsAllowedDuplicate(group.Key, [.. group.Select(declaration => declaration.File)]))
                .Select(group => group.Key);
            Assert.Empty(duplicates);
        }

        /// <summary>The generator cannot reference Core, so <c>Catalog/Engine/ExperimentalApis.cs</c> re-declares this id (ADR-0017 decision, implementation notes).</summary>
        private const string AllowedDuplicateCode = "NPXE0004";

        private static readonly string[] AllowedDuplicateFiles =
        [
            "NetPrints.Core/ExperimentalApiIds.cs",
            "NetPrints.Catalog/Engine/ExperimentalApis.cs",
        ];

        /// <summary>The one allowed duplicate is exactly two declarations of <c>NPXE0004</c>, one in each of the two named files.</summary>
        internal static bool IsAllowedDuplicate(string code, IReadOnlyCollection<string> files) =>
            code == AllowedDuplicateCode
            && files.Count == AllowedDuplicateFiles.Length
            && files.Select(file => file.Replace('\\', '/')).Order(StringComparer.Ordinal).SequenceEqual(AllowedDuplicateFiles.Order(StringComparer.Ordinal));

        [GeneratedRegex(@"const string \w+ = ""(?<code>(?:NP[TDXWCSAHIKR])\d{3,}|NPXE\d{4})""")]
        private static partial Regex DiagnosticCodeDeclarationPattern();

        /// <summary>
        /// ADR-0003 "no sync-over-async": no analyzer flags blocking on a <see cref="Task"/> result, so
        /// this parses every <c>src/**/*.cs</c> and <c>tests/**/*.cs</c> file with Roslyn and fails on a
        /// <c>.Wait()</c> call, a <c>.GetAwaiter().GetResult()</c> chain, or a <c>.Result</c> access whose
        /// receiver looks like a <see cref="Task"/> (R1-22/R2-18/R3-15). The allowlist is empty by design
        /// and can only shrink for pre-existing vendored code; a fresh site is fixed, not added.
        /// </summary>
        private static readonly HashSet<string> SyncOverAsyncAllowlist = new(StringComparer.Ordinal);

        /// <summary>True for a <c>.Result</c> access whose receiver is itself named like a <see cref="Task"/>
        /// (e.g. <c>stdOutTask.Result</c>) or an <c>…Async(...)</c> call's result — as opposed to an
        /// unrelated domain <c>Result</c> property (e.g. a dialog view model's own <c>Result</c>).</summary>
        private static bool LooksLikeTaskResultAccess(MemberAccessExpressionSyntax memberAccess)
        {
            if (memberAccess.Name.Identifier.Text != "Result")
            {
                return false;
            }

            if (memberAccess.Expression.ToString().Contains("Task", StringComparison.Ordinal))
            {
                return true;
            }

            return memberAccess.Expression is InvocationExpressionSyntax { Expression: var target }
                && target.ToString().EndsWith("Async", StringComparison.Ordinal);
        }

        [Fact]
        public void NoSyncOverAsync()
        {
            string repoRoot = SampleProjectFactory.FindRepositoryRoot();
            var offenders = new List<string>();
            int parsedFileCount = 0;

            foreach (string path in EnumerateSourceAndTestFiles(repoRoot))
            {
                parsedFileCount++;
                string relativePath = Path.GetRelativePath(repoRoot, path).Replace('\\', '/');
                SyntaxNode root = CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path, cancellationToken: TestContext.Current.CancellationToken).GetRoot(TestContext.Current.CancellationToken);

                foreach (InvocationExpressionSyntax invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    bool isWait = invocation.Expression is MemberAccessExpressionSyntax { Name.Identifier.Text: "Wait" };
                    bool isGetAwaiterGetResult = invocation.Expression is MemberAccessExpressionSyntax
                    {
                        Name.Identifier.Text: "GetResult",
                        Expression: InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "GetAwaiter" } },
                    };
                    if (!isWait && !isGetAwaiterGetResult)
                    {
                        continue;
                    }

                    int line = invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    string key = $"{relativePath}:{line}";
                    if (!SyncOverAsyncAllowlist.Contains(key))
                    {
                        offenders.Add($"{key}: {invocation}");
                    }
                }

                foreach (MemberAccessExpressionSyntax memberAccess in root.DescendantNodes().OfType<MemberAccessExpressionSyntax>()
                    .Where(LooksLikeTaskResultAccess))
                {
                    int line = memberAccess.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    string key = $"{relativePath}:{line}";
                    if (!SyncOverAsyncAllowlist.Contains(key))
                    {
                        offenders.Add($"{key}: {memberAccess}");
                    }
                }
            }

            Assert.True(parsedFileCount > 0, "Expected to scan at least one src/ or tests/ file.");
            Assert.Empty(offenders);
        }

        /// <summary>
        /// No <c>.props</c>, <c>.targets</c> or <c>.csproj</c> file anywhere in the repository (except
        /// <c>legacy/</c>, kept for reference and not built) lowers the warning bar the root
        /// <c>Directory.Build.props</c> sets (<c>TreatWarningsAsErrors</c>) or the curated analyzer
        /// severities in <c>.editorconfig</c> establish: a <c>&lt;NoWarn&gt;</c>/<c>&lt;WarningsNotAsErrors&gt;</c>
        /// entry, <c>&lt;TreatWarningsAsErrors&gt;false&lt;/TreatWarningsAsErrors&gt;</c>, an explicit
        /// <c>&lt;WarningLevel&gt;</c> override, or an unlisted <c>&lt;MSBuildWarningsNotAsErrors&gt;</c>/
        /// <c>&lt;MSBuildWarningsAsMessages&gt;</c> engine-level suppression (<see cref="BuildPropertyAllowlist"/>)
        /// would each silently do that project-wide, bypassing NPH004's
        /// per-site ledger entirely.
        /// </summary>
        [Fact]
        public void NoUnlistedBuildWarningSuppressions()
        {
            string root = SampleProjectFactory.FindRepositoryRoot();
            var offenders = new List<string>();
            int parsedFileCount = 0;

            foreach ((string relativePath, XDocument document) in BuildFileRules.Load(root, "*.props", "*.targets", "*.csproj"))
            {
                parsedFileCount++;
                offenders.AddRange(BuildFileRules.WarningOffenders(relativePath, document));
            }

            Assert.True(parsedFileCount > 0, "Expected to scan at least one .props/.targets/.csproj file.");
            Assert.Empty(offenders);
        }

        /// <summary>ADR-0017 and ADR-0003: the opt-in line exists exactly once, in <c>Directory.Build.targets</c>, and the only other <c>&lt;NoWarn&gt;</c> holders are the projects of ADR-0003's build-property allowances, each with only the codes its row names.</summary>
        [Fact]
        public void TheOnlyNoWarnsAreTheOptInLineAndExtensionProjects()
        {
            string root = SampleProjectFactory.FindRepositoryRoot();
            var holders = new List<string>();
            foreach ((string relativePath, XDocument document) in BuildFileRules.Load(root, "*.props", "*.targets", "*.csproj"))
            {
                if (BuildFileRules.NoWarnElements(document).Any())
                {
                    holders.Add(relativePath);
                }
            }

            Assert.Contains(BuildFileRules.OptInTargetsFile, holders);
            foreach (string holder in holders.Where(holder => holder != BuildFileRules.OptInTargetsFile))
            {
                XDocument document = XDocument.Load(Path.Combine(root, holder));
                Assert.True(
                    BuildFileRules.NoWarnElements(document).All(noWarn => BuildFileRules.IsExtensionProjectNoWarn(holder, noWarn)),
                    $"{holder} holds a <NoWarn> that ADR-0003's build-property allowances do not list");
            }

            XDocument targets = XDocument.Load(Path.Combine(root, BuildFileRules.OptInTargetsFile));
            Assert.True(
                BuildFileRules.NoWarnElements(targets).All(noWarn => BuildFileRules.IsOptInNoWarn(BuildFileRules.OptInTargetsFile, targets, noWarn)),
                $"{BuildFileRules.OptInTargetsFile} must contain exactly <NoWarn>{BuildFileRules.OptInNoWarnValue}</NoWarn>");
        }

        /// <summary>ADR-0003 "Build-property allowances": the projects <see cref="BuildFileRules"/> lets hold their own <c>NoWarn</c> are exactly the <c>NoWarn</c> rows of the ADR's table, with the codes each row names, apart from the opt-in line.</summary>
        [Fact]
        public void EveryExtensionNoWarnAllowanceIsARowOfAdr0003()
        {
            string adr = File.ReadAllText(Path.Combine(SampleProjectFactory.FindRepositoryRoot(), "docs", "adr", "0003-analyzer-promotion-and-string-literal-discipline.md"));
            (string File, bool PackLayout, bool OptIn)[] rows =
            [
                .. adr.Split('\n')
                    .Where(line => line.StartsWith("| `", StringComparison.Ordinal) && line.Contains(" | `NoWarn` | ", StringComparison.Ordinal))
                    .Select(line => line.Split('|'))
                    .Select(cells => (File: cells[1].Split('`')[1], Codes: cells[3]))
                    .Where(row => row.File != BuildFileRules.OptInTargetsFile)
                    .Select(row => (row.File, PackLayout: row.Codes.Contains("NU5100", StringComparison.Ordinal), OptIn: row.Codes.Contains("NPXE", StringComparison.Ordinal)))
                    .OrderBy(row => row.File, StringComparer.Ordinal),
            ];

            Assert.Equal([.. BuildFileRules.ExtensionProjectNoWarnRows.OrderBy(row => row.File, StringComparer.Ordinal)], rows);
        }

        /// <summary>ADR-0017: opt-ins are per project, so an item in a props or targets file (a repo-wide or inherited opt-in) is rejected.</summary>
        [Fact]
        public void ExperimentalOptInItemsLiveOnlyInProjectFiles()
        {
            string root = SampleProjectFactory.FindRepositoryRoot();

            string[] offenders =
            [
                .. BuildFileRules.Load(root, "*.props", "*.targets")
                    .Where(file => BuildFileRules.Named(file.Document, BuildFileRules.OptInItem).Any())
                    .Select(file => file.RelativePath),
            ];

            Assert.Empty(offenders);
        }

        /// <summary>ADR-0017: every opt-in item names an id <c>ExperimentalApiIds</c> declares, so an unknown or graduated (stale) id fails.</summary>
        [Fact]
        public void EveryExperimentalOptInIsADeclaredId()
        {
            string root = SampleProjectFactory.FindRepositoryRoot();
            string idsFile = Path.Combine(root, "src", "NetPrints.Core", "ExperimentalApiIds.cs");
            Assert.True(File.Exists(idsFile), "src/NetPrints.Core/ExperimentalApiIds.cs must declare the experimental ids");
            HashSet<string> declared =
            [
                .. ExperimentalIdDeclarationPattern().Matches(File.ReadAllText(idsFile)).Select(match => match.Groups["id"].Value),
            ];
            Assert.NotEmpty(declared);

            var offenders = new List<string>();
            foreach ((string relativePath, XDocument document) in BuildFileRules.Load(root, "*.csproj"))
            {
                foreach (string id in BuildFileRules.OptInIds(document).Where(id => !declared.Contains(id)))
                {
                    offenders.Add($"{relativePath}: {id}");
                }
            }

            Assert.Empty(offenders);
        }

        /// <summary>ADR-0017: no <c>.editorconfig</c> or globalconfig entry sets a severity for an <c>NPXE</c> id.</summary>
        [Fact]
        public void NoEditorConfigSeverityForAnExperimentalDiagnostic()
        {
            string root = SampleProjectFactory.FindRepositoryRoot();

            string[] offenders =
            [
                .. new[] { ".editorconfig", "*.globalconfig" }
                    .SelectMany(pattern => BuildFileRules.EnumeratePaths(root, pattern))
                    .Where(path => ExperimentalSeverityPattern().IsMatch(File.ReadAllText(path)))
                    .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/')),
            ];

            Assert.Empty(offenders);
        }

        [GeneratedRegex(@"const string \w+ = ""(?<id>NPXE\d{4})""")]
        private static partial Regex ExperimentalIdDeclarationPattern();

        [GeneratedRegex(@"^\s*dotnet_diagnostic\.NPXE", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
        private static partial Regex ExperimentalSeverityPattern();

        [GeneratedRegex(@"^\[(?<section>.+)\]$")]
        private static partial Regex EditorConfigSectionPattern();

        [GeneratedRegex(@"^dotnet_diagnostic\.(?<rule>[A-Za-z0-9_-]+)\.severity\s*=\s*(?<severity>\S+)")]
        private static partial Regex EditorConfigSeverityPattern();

        [GeneratedRegex(@"<(?:\w+:)?Popup(?=[\s/>])")]
        private static partial Regex RawPopupTagPattern();

        /// <summary>
        /// ADR-0004: every canvas popup goes through <c>CanvasPopup</c>, which anchors itself at the
        /// pointer and centralizes Esc/light-dismiss/focus behavior; nothing else declares a raw
        /// Avalonia <c>Popup</c>. <c>CanvasPopup</c> itself is a code-only control (no .axaml), so
        /// this needs no exemption for its own file.
        /// </summary>
        [Fact]
        public void NoRawPopupOutsideCanvasPopup()
        {
            string src = Path.Combine(SampleProjectFactory.FindRepositoryRoot(), "src");
            string[] axamlFiles = EnumerateSourceFiles(src, "*.axaml");
            Regex pattern = RawPopupTagPattern();

            string[] offenders = [.. axamlFiles
                .Where(path => pattern.IsMatch(File.ReadAllText(path)))
                .Select(path => Path.GetRelativePath(src, path))];

            Assert.True(axamlFiles.Length > 0, "Expected to scan at least one src/**/*.axaml file.");
            Assert.Empty(offenders);
        }

        /// <summary>
        /// Every <c>dotnet_diagnostic.&lt;rule&gt;.severity</c> entry below <c>error</c> in a
        /// <c>[src/...]</c>-headed <c>.editorconfig</c> section must be in this explicit allowlist,
        /// which mirrors ADR-0003's severity-policy table: the "warning"-tier researched rule set and
        /// the locale-sensitive/collection-exposure rules that stay below <c>error</c> in
        /// <c>src/**.cs</c>, the legacy-model carve-out for <c>CA1002</c>/<c>CA2227</c>, and the layered
        /// <c>VSTHRD111 = none</c> entries for UI-thread-affine paths. A repo-wide <c>[*.cs]</c> section
        /// (the <c>suggestion</c>-by-default rule set every non-curated Sonar/IDISP/VSTHRD rule ships
        /// at, including in <c>tests/</c>) is out of scope: only a section whose glob starts with
        /// <c>src/</c> is checked.
        /// </summary>
        private static readonly HashSet<(string Section, string Rule, string Severity)> EditorConfigNonErrorSeverityAllowlist = new()
        {
            ("src/**.cs", "CA1068", "warning"),
            ("src/**.cs", "CA1861", "warning"),
            ("src/**.cs", "CA1510", "warning"),
            ("src/**.cs", "CA1511", "warning"),
            ("src/**.cs", "CA1512", "warning"),
            ("src/**.cs", "CA1513", "warning"),
            ("src/**.cs", "S2139", "warning"),
            ("src/**.cs", "CA1065", "warning"),
            ("src/**.cs", "CA1307", "warning"),
            ("src/**.cs", "CA1309", "warning"),
            ("src/**.cs", "CA1851", "warning"),
            ("src/**.cs", "CA1827", "warning"),
            ("src/**.cs", "CA1829", "warning"),
            ("src/**.cs", "CA1860", "warning"),
            ("src/**.cs", "CA1852", "warning"),
            ("src/**.cs", "S2933", "warning"),
            ("src/**.cs", "CA1848", "warning"),
            ("src/**.cs", "CA1727", "warning"),
            ("src/**.cs", "CA1305", "warning"),
            ("src/**.cs", "CA1310", "warning"),
            ("src/**.cs", "CA1311", "warning"),
            ("src/{NetPrints.Core,NetPrints.Reflection}/**.cs", "CA1002", "suggestion"),
            ("src/{NetPrints.Core,NetPrints.Reflection}/**.cs", "CA2227", "suggestion"),
            ("src/NetPrints.Editor/**ViewModel.cs", "VSTHRD111", "none"),
            ("src/NetPrints.Editor/**.axaml.cs", "VSTHRD111", "none"),
            ("src/NetPrints.Editor/ModelSync/*.cs", "VSTHRD111", "none"),
            ("src/NetPrints.Editor/Hosting/EditorComposition.cs", "VSTHRD111", "none"),
            ("src/NetPrints.Editor/Hosting/ShutdownCoordinator.cs", "VSTHRD111", "none"),
            ("src/NetPrints.Editor/Hosting/Avalonia/**.cs", "VSTHRD111", "none"),
            ("src/NetPrints.Editor/Graph/GraphDragDrop.cs", "VSTHRD111", "none"),
            ("src/NetPrints.Desktop/**.cs", "VSTHRD111", "none"),
        };

        [Fact]
        public void EveryNonErrorSrcSeverityInEditorConfigIsAllowlisted()
        {
            string editorConfigPath = Path.Combine(SampleProjectFactory.FindRepositoryRoot(), ".editorconfig");
            string? section = null;
            var offenders = new List<string>();
            int checkedCount = 0;

            foreach (string line in File.ReadAllLines(editorConfigPath))
            {
                string trimmed = line.Trim();
                Match header = EditorConfigSectionPattern().Match(trimmed);
                if (header.Success)
                {
                    section = header.Groups["section"].Value;
                    continue;
                }

                Match severityMatch = EditorConfigSeverityPattern().Match(trimmed);
                if (!severityMatch.Success || section is null || !section.StartsWith("src/", StringComparison.Ordinal))
                {
                    continue;
                }

                string rule = severityMatch.Groups["rule"].Value;
                string severity = severityMatch.Groups["severity"].Value;
                checkedCount++;

                if (severity != "error" && !EditorConfigNonErrorSeverityAllowlist.Contains((section, rule, severity)))
                {
                    offenders.Add($"[{section}] dotnet_diagnostic.{rule}.severity = {severity}");
                }
            }

            Assert.True(checkedCount > 0, "Expected to check at least one src/... severity entry in .editorconfig.");
            Assert.Empty(offenders);
        }

        [Fact]
        public void NoRasterIconsInSource()
        {
            string src = Path.Combine(SampleProjectFactory.FindRepositoryRoot(), "src");
            string[] pngFiles = [.. Directory.EnumerateFiles(src, "*.png", SearchOption.AllDirectories)
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal)];

            Assert.True(pngFiles.Length == 0, $"Raster PNG files must be replaced with vector icons from IconIds (T092b): {string.Join("; ", pngFiles.Select(p => Path.GetRelativePath(src, p)))}");
        }

        /// <summary>FR-100: the other agent tool's XAML rule is a short pointer to AGENTS.md and the three skills, never a second copy of them.</summary>
        [Fact]
        public void AgentRulesPointToTheSkills()
        {
            string root = SampleProjectFactory.FindRepositoryRoot();
            string rule = Path.Combine(root, ".agents", "rules", "xaml.md");
            Assert.True(File.Exists(rule), "Expected .agents/rules/xaml.md.");

            string[] lines = File.ReadAllLines(rule);
            string text = string.Join('\n', lines);
            Assert.True(lines.Length <= 15, $".agents/rules/xaml.md has {lines.Length} lines; at most 15.");
            Assert.Contains("AGENTS.md", text, StringComparison.Ordinal);
            foreach (string skill in new[] { "avalonia-xaml", "avalonia-styling", "avalonia-behaviors" })
            {
                string path = $".claude/skills/{skill}/SKILL.md";
                Assert.Contains(path, text, StringComparison.Ordinal);
                Assert.True(File.Exists(Path.Combine(root, path)), $"{path} does not exist.");
            }

            Assert.False(Directory.Exists(Path.Combine(root, ".agents", "skills")), ".agents/skills must not exist: no forked copy of a skill.");
        }
    }
}
