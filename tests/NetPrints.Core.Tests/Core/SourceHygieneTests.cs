using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NetPrints.Tests.Samples;
using Xunit;

namespace NetPrints.Tests.Core
{
    /// <summary>FR-058: the product never resolves references from a hard-coded Windows install path.</summary>
    public partial class SourceHygieneTests
    {
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
    }
}
