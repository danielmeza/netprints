using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
        /// A stable <c>NPTnnn</c>/<c>NPDnnn</c>/<c>NPXnnn</c>/<c>NPWnnn</c> diagnostic code is declared
        /// exactly once, as a named constant, and referenced everywhere else
        /// (compilation-and-diagnostics.md §2): gates the smell found in
        /// <c>TranslationDiagnosticCodes.cs</c>'s introduction, where the translator's <c>NPT</c> codes
        /// were raw literals scattered across several files. Unlike the declaration itself, a raw
        /// literal is not allowed even inside a declaring file: only the one line that declares a code
        /// is exempt from the "no raw literal" check below.
        /// </summary>
        [Fact]
        public void NoRawDiagnosticCodeLiteralsOutsideTheirConstants()
        {
            string src = Path.Combine(SampleProjectFactory.FindRepositoryRoot(), "src");
            Regex codePattern = DiagnosticCodeLiteralPattern();
            Regex declarationPattern = DiagnosticCodeDeclarationPattern();

            // Files that declare one or more of these codes as a named constant. NPW is split across
            // ProjectSystemException (NPW001, NPW003) and ProjectMessage (the rest), same as NPD/NPX/NPT.
            string[] declaringFiles =
            [
                Path.Combine("NetPrints.Core", "Translator", "TranslationDiagnosticCodes.cs"),
                Path.Combine("NetPrints.Extensibility", "Loading", "ExtensionDiagnosticCodes.cs"),
                Path.Combine("NetPrints.Serialization", "DocumentIssue.cs"),
                Path.Combine("NetPrints.Generation", "GraphCodeGenerator.cs"),
                Path.Combine("NetPrints.Core", "Projects", "ProjectSystemException.cs"),
                Path.Combine("NetPrints.Core", "Projects", "ProjectMessage.cs"),
            ];

            string[] sourceFiles = [.. Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))];

            var declaredCodes = new List<string>();
            var offenders = new List<string>();

            foreach (string path in sourceFiles)
            {
                string relativePath = Path.GetRelativePath(src, path);
                bool isDeclaringFile = declaringFiles.Any(f => path.EndsWith(f, StringComparison.Ordinal));
                bool foundRawLiteral = false;

                foreach (string line in File.ReadAllLines(path))
                {
                    string trimmed = line.TrimStart();
                    if (trimmed.StartsWith("///", StringComparison.Ordinal))
                    {
                        // XML doc prose (e.g. "for example \"NPT006\"") documents the format, not a duplicated literal.
                        continue;
                    }

                    Match declaration = declarationPattern.Match(line);
                    if (declaration.Success)
                    {
                        Assert.True(isDeclaringFile, $"{relativePath}: a diagnostic code constant must be declared in one of the designated files.");
                        declaredCodes.Add(declaration.Groups["code"].Value);
                        continue;
                    }

                    if (codePattern.IsMatch(line))
                    {
                        foundRawLiteral = true;
                    }
                }

                if (foundRawLiteral)
                {
                    offenders.Add(relativePath);
                }
            }

            Assert.Empty(offenders);

            IEnumerable<string> duplicates = declaredCodes.GroupBy(code => code, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key);
            Assert.Empty(duplicates);
        }

        [GeneratedRegex(@"""(?:NPT|NPD|NPX|NPW)\d{3,}""")]
        private static partial Regex DiagnosticCodeLiteralPattern();

        [GeneratedRegex(@"const string \w+ = ""(?<code>(?:NPT|NPD|NPX|NPW)\d{3,})""")]
        private static partial Regex DiagnosticCodeDeclarationPattern();

        /// <summary>
        /// ADR-0003 "no `!`" (AGENTS.md "Nullable reference types"): no analyzer flags the
        /// null-forgiving operator, so this parses every <c>src/**/*.cs</c> file with Roslyn and fails
        /// on any <see cref="SyntaxKind.SuppressNullableWarningExpression"/> node outside
        /// <see cref="NullForgivingAllowlist"/>. The allowlist is empty by design: fix the cause
        /// instead of adding to it.
        /// </summary>
        private static readonly HashSet<string> NullForgivingAllowlist = new(StringComparer.Ordinal);

        [Fact]
        public void NoNullForgivingOperator()
        {
            string src = Path.Combine(SampleProjectFactory.FindRepositoryRoot(), "src");
            string[] sourceFiles = EnumerateSourceFiles(src, "*.cs");
            var offenders = new List<string>();
            int parsedFileCount = 0;

            foreach (string path in sourceFiles)
            {
                SyntaxNode root = CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path, cancellationToken: TestContext.Current.CancellationToken).GetRoot(TestContext.Current.CancellationToken);
                parsedFileCount++;
                string relativePath = Path.GetRelativePath(src, path);

                foreach (PostfixUnaryExpressionSyntax node in root.DescendantNodes()
                    .OfType<PostfixUnaryExpressionSyntax>()
                    .Where(n => n.IsKind(SyntaxKind.SuppressNullableWarningExpression)))
                {
                    int line = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    string key = $"{relativePath}:{line}";
                    if (!NullForgivingAllowlist.Contains(key))
                    {
                        offenders.Add($"{key}: {node}");
                    }
                }
            }

            Assert.True(parsedFileCount > 0, "Expected to parse at least one src/**/*.cs file.");
            Assert.Empty(offenders);
        }

        /// <summary>
        /// ADR-0003 "Real fixes vs. suppression": every suppression mechanism in <c>src/</c> — a
        /// <c>#pragma warning disable</c>, <c>[SuppressMessage]</c> (short or <c>Attribute</c>-suffixed,
        /// qualified or not) or a nullable-context escape (<c>#nullable disable</c>/<c>restore</c>) in a
        /// <c>.cs</c> file — must match a row in ADR-0003's suppression ledger. A <c>[SuppressMessage]</c>
        /// is keyed by its file, containing member and rule id (not file and rule alone), so a second
        /// suppression of the same rule on a different member of the same file is still unlisted.
        /// Tighter than <c>S1309</c>: per rule and per site, not per file. <c>&lt;NoWarn&gt;</c> and the
        /// rest of a project's warning configuration are covered by
        /// <see cref="NoUnlistedBuildWarningSuppressions"/> instead, since those live in
        /// <c>.csproj</c>/<c>.props</c>/<c>.targets</c> files, not <c>.cs</c> ones.
        /// </summary>
        private static readonly HashSet<(string File, string Member, string Rule)> SuppressionAllowlist = new()
        {
            ("NetPrints.Editor/ClassEditor/ClassEditorVM.cs", "OpenGraph", "IDISP003"),
            ("NetPrints.Editor/ClassEditor/ClassEditorVM.cs", "DropDetachedState", "IDISP003"),
            ("NetPrints.Editor/ClassEditor/ClassEditorVM.cs", "Dispose", "IDISP003"),
            ("NetPrints.Editor/Graph/GraphDragDrop.cs", "Moved", "VSTHRD100"),
            ("NetPrints.Editor/Graph/GridBackground.cs", "Render", "IDISP004"),
            ("NetPrints.Editor/Hosting/Automation/AutomationAgent.cs", "ServeAsync", "IDISP007"),
        };

        [Fact]
        public void NoUnlistedSuppressions()
        {
            string src = Path.Combine(SampleProjectFactory.FindRepositoryRoot(), "src");
            var offenders = new List<string>();
            int parsedFileCount = 0;

            foreach (string path in EnumerateSourceFiles(src, "*.cs"))
            {
                parsedFileCount++;
                string relativePath = Path.GetRelativePath(src, path).Replace('\\', '/');
                SyntaxNode root = CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path, cancellationToken: TestContext.Current.CancellationToken).GetRoot(TestContext.Current.CancellationToken);

                foreach (var trivia in root.DescendantTrivia().Where(t => t.IsKind(SyntaxKind.PragmaWarningDirectiveTrivia)))
                {
                    offenders.Add($"{relativePath}: unlisted #pragma warning ({trivia})");
                }

                foreach (var trivia in root.DescendantTrivia().Where(t => t.IsKind(SyntaxKind.NullableDirectiveTrivia)))
                {
                    if (trivia.GetStructure() is NullableDirectiveTriviaSyntax { SettingToken: var setting }
                        && (setting.IsKind(SyntaxKind.DisableKeyword) || setting.IsKind(SyntaxKind.RestoreKeyword)))
                    {
                        offenders.Add($"{relativePath}: unlisted #nullable {setting.Text}");
                    }
                }

                foreach (AttributeSyntax attribute in root.DescendantNodes().OfType<AttributeSyntax>()
                    .Where(a => IsSuppressMessageAttribute(a)))
                {
                    var arguments = attribute.ArgumentList?.Arguments ?? default;
                    string? ruleId = arguments.Count > 1 ? arguments[1].Expression.ToString().Trim('"') : null;
                    string? member = ContainingMemberName(attribute);
                    string justification = attribute.ToString();

                    if (ruleId is null || member is null || !SuppressionAllowlist.Contains((relativePath, member, ruleId)))
                    {
                        offenders.Add($"{relativePath}: unlisted [SuppressMessage] for {ruleId ?? "?"} on {member ?? "?"}");
                    }
                    else if (!justification.Contains("ADR-0003", StringComparison.Ordinal))
                    {
                        offenders.Add($"{relativePath}: [SuppressMessage] for {ruleId} has no ADR-0003 justification");
                    }
                }
            }

            Assert.True(parsedFileCount > 0, "Expected to scan at least one src/ file.");
            Assert.Empty(offenders);
        }

        /// <summary>
        /// Matches <c>[SuppressMessage(...)]</c> and <c>[SuppressMessageAttribute(...)]</c>, with or
        /// without a namespace qualifier: <see cref="AttributeSyntax.Name"/> renders the qualifier too
        /// (e.g. <c>System.Diagnostics.CodeAnalysis.SuppressMessageAttribute</c>), so only the suffix
        /// after stripping a trailing "Attribute" is compared.
        /// </summary>
        private static bool IsSuppressMessageAttribute(AttributeSyntax attribute)
        {
            string name = attribute.Name.ToString();
            if (name.EndsWith("Attribute", StringComparison.Ordinal))
            {
                name = name[..^"Attribute".Length];
            }

            return name.EndsWith("SuppressMessage", StringComparison.Ordinal);
        }

        /// <summary>The name of the method, property or constructor <paramref name="attribute"/> is declared on, or null.</summary>
        private static string? ContainingMemberName(AttributeSyntax attribute)
        {
            for (SyntaxNode? node = attribute.Parent; node is not null; node = node.Parent)
            {
                switch (node)
                {
                    case MethodDeclarationSyntax method:
                        return method.Identifier.Text;
                    case PropertyDeclarationSyntax property:
                        return property.Identifier.Text;
                    case ConstructorDeclarationSyntax constructor:
                        return constructor.Identifier.Text;
                }
            }

            return null;
        }

        [GeneratedRegex(@"<NoWarn>")]
        private static partial Regex NoWarnPattern();

        [GeneratedRegex(@"<WarningsNotAsErrors>")]
        private static partial Regex WarningsNotAsErrorsPattern();

        [GeneratedRegex(@"<TreatWarningsAsErrors>\s*false\s*</TreatWarningsAsErrors>", RegexOptions.IgnoreCase)]
        private static partial Regex TreatWarningsAsErrorsFalsePattern();

        [GeneratedRegex(@"<WarningLevel>")]
        private static partial Regex WarningLevelPattern();

        [GeneratedRegex(@"<MSBuildWarningsNotAsErrors>(?<value>[^<]*)</MSBuildWarningsNotAsErrors>")]
        private static partial Regex MSBuildWarningsNotAsErrorsPattern();

        [GeneratedRegex(@"<MSBuildWarningsAsMessages>(?<value>[^<]*)</MSBuildWarningsAsMessages>")]
        private static partial Regex MSBuildWarningsAsMessagesPattern();

        /// <summary>
        /// ADR-0003 "Build-property allowances": an engine-level <c>&lt;MSBuildWarningsNotAsErrors&gt;</c>
        /// or <c>&lt;MSBuildWarningsAsMessages&gt;</c> entry lowers the warning bar the same way a
        /// compiler-level <c>&lt;NoWarn&gt;</c> does, just for MSBuild's own diagnostics instead of
        /// Roslyn's. Allowed only for a warning code listed here, keyed by the file and the code.
        /// </summary>
        private static readonly HashSet<(string File, string Code)> BuildPropertyAllowlist = new()
        {
            // MinVer warns (MINVER1001) when there is no .git history (a source archive); release
            // contract §1 requires the build to stay green in that case.
            ("Directory.Build.props", "MINVER1001"),
        };

        /// <summary>Splits a semicolon-separated MSBuild property value into its literal warning codes, dropping a self-referencing <c>$(...)</c> expansion.</summary>
        private static IEnumerable<string> ExtractWarningCodes(string value) =>
            value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(token => !token.StartsWith("$(", StringComparison.Ordinal));

        /// <summary>
        /// No <c>.props</c>, <c>.targets</c> or <c>.csproj</c> file anywhere in the repository (except
        /// <c>legacy/</c>, kept for reference and not built) lowers the warning bar the root
        /// <c>Directory.Build.props</c> sets (<c>TreatWarningsAsErrors</c>) or the curated analyzer
        /// severities in <c>.editorconfig</c> establish: a <c>&lt;NoWarn&gt;</c>/<c>&lt;WarningsNotAsErrors&gt;</c>
        /// entry, <c>&lt;TreatWarningsAsErrors&gt;false&lt;/TreatWarningsAsErrors&gt;</c>, an explicit
        /// <c>&lt;WarningLevel&gt;</c> override, or an unlisted <c>&lt;MSBuildWarningsNotAsErrors&gt;</c>/
        /// <c>&lt;MSBuildWarningsAsMessages&gt;</c> engine-level suppression (<see cref="BuildPropertyAllowlist"/>)
        /// would each silently do that project-wide, bypassing <see cref="NoUnlistedSuppressions"/>'s
        /// per-site ledger entirely.
        /// </summary>
        [Fact]
        public void NoUnlistedBuildWarningSuppressions()
        {
            string root = SampleProjectFactory.FindRepositoryRoot();
            var offenders = new List<string>();
            int parsedFileCount = 0;

            IEnumerable<string> files = new[] { "*.props", "*.targets", "*.csproj" }
                .SelectMany(pattern => Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories))
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    && !path.Contains($"{Path.DirectorySeparatorChar}legacy{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

            foreach (string path in files)
            {
                parsedFileCount++;
                string relativePath = Path.GetRelativePath(root, path).Replace('\\', '/');
                string text = File.ReadAllText(path);

                if (NoWarnPattern().IsMatch(text))
                {
                    offenders.Add($"{relativePath}: <NoWarn>");
                }

                if (WarningsNotAsErrorsPattern().IsMatch(text))
                {
                    offenders.Add($"{relativePath}: <WarningsNotAsErrors>");
                }

                if (TreatWarningsAsErrorsFalsePattern().IsMatch(text))
                {
                    offenders.Add($"{relativePath}: <TreatWarningsAsErrors>false</TreatWarningsAsErrors>");
                }

                if (WarningLevelPattern().IsMatch(text))
                {
                    offenders.Add($"{relativePath}: <WarningLevel>");
                }

                foreach (Match match in MSBuildWarningsNotAsErrorsPattern().Matches(text))
                {
                    foreach (string code in ExtractWarningCodes(match.Groups["value"].Value))
                    {
                        if (!BuildPropertyAllowlist.Contains((relativePath, code)))
                        {
                            offenders.Add($"{relativePath}: unlisted <MSBuildWarningsNotAsErrors> {code}");
                        }
                    }
                }

                foreach (Match match in MSBuildWarningsAsMessagesPattern().Matches(text))
                {
                    foreach (string code in ExtractWarningCodes(match.Groups["value"].Value))
                    {
                        if (!BuildPropertyAllowlist.Contains((relativePath, code)))
                        {
                            offenders.Add($"{relativePath}: unlisted <MSBuildWarningsAsMessages> {code}");
                        }
                    }
                }
            }

            Assert.True(parsedFileCount > 0, "Expected to scan at least one .props/.targets/.csproj file.");
            Assert.Empty(offenders);
        }

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
            ("src/NetPrints.Editor/**VM.cs", "VSTHRD111", "none"),
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
    }
}
