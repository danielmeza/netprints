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
                Path.Combine("NetPrints.Generator", "GraphCodeGenerator.cs"),
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
        /// <c>#pragma warning disable</c> or <c>[SuppressMessage]</c> in a <c>.cs</c> file, or a
        /// <c>&lt;NoWarn&gt;</c> in a <c>.csproj</c> — must match a row in ADR-0003's suppression
        /// ledger (by file and rule id) and its justification must reference "ADR-0003". Tighter than
        /// <c>S1309</c>: per rule and per site, not per file.
        /// </summary>
        private static readonly HashSet<(string File, string Rule)> SuppressionAllowlist = new()
        {
            ("NetPrints.Editor/ClassEditor/ClassEditorVM.cs", "IDISP003"),
            ("NetPrints.Editor/Graph/GraphDragDrop.cs", "VSTHRD100"),
            ("NetPrints.Editor/Graph/GridBackground.cs", "IDISP004"),
            ("NetPrints.Editor/Hosting/Automation/AutomationAgent.cs", "IDISP007"),
        };

        [GeneratedRegex(@"<NoWarn>")]
        private static partial Regex NoWarnPattern();

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

                foreach (AttributeSyntax attribute in root.DescendantNodes().OfType<AttributeSyntax>()
                    .Where(a => a.Name.ToString().EndsWith("SuppressMessage", StringComparison.Ordinal)))
                {
                    var arguments = attribute.ArgumentList?.Arguments ?? default;
                    string? ruleId = arguments.Count > 1 ? arguments[1].Expression.ToString().Trim('"') : null;
                    string justification = attribute.ToString();

                    if (ruleId is null || !SuppressionAllowlist.Contains((relativePath, ruleId)))
                    {
                        offenders.Add($"{relativePath}: unlisted [SuppressMessage] for {ruleId ?? "?"}");
                    }
                    else if (!justification.Contains("ADR-0003", StringComparison.Ordinal))
                    {
                        offenders.Add($"{relativePath}: [SuppressMessage] for {ruleId} has no ADR-0003 justification");
                    }
                }
            }

            foreach (string path in EnumerateSourceFiles(src, "*.csproj"))
            {
                parsedFileCount++;
                string relativePath = Path.GetRelativePath(src, path).Replace('\\', '/');
                if (NoWarnPattern().IsMatch(File.ReadAllText(path)))
                {
                    offenders.Add($"{relativePath}: unlisted <NoWarn>");
                }
            }

            Assert.True(parsedFileCount > 0, "Expected to scan at least one src/ file.");
            Assert.Empty(offenders);
        }
    }
}
