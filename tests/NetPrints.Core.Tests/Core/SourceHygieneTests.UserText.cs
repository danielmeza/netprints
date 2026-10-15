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
    public partial class SourceHygieneTests
    {
        private static readonly Regex RoadmapCode = new(@"\b(P[0-9][ab]?|ADR-?[0-9]+|T[01][0-9]{2}|FR-[0-9]+|SC-[0-9]+|US[0-9]+|PAR-[0-9]+)\b", RegexOptions.CultureInvariant);

        /// <summary>User-visible text never names a roadmap phase, task, requirement or ADR: those live in comments and docs.</summary>
        [Fact]
        public void NoUserVisibleStringNamesARoadmapCode()
        {
            string src = Path.Combine(SampleProjectFactory.FindRepositoryRoot(), "src");
            var offenders = new List<string>();

            foreach (string path in EnumerateSourceFiles(src, "*.cs"))
            {
                SyntaxNode root = CSharpSyntaxTree.ParseText(File.ReadAllText(path), cancellationToken: TestContext.Current.CancellationToken).GetRoot(TestContext.Current.CancellationToken);
                foreach (SyntaxToken token in root.DescendantTokens())
                {
                    bool text = token.IsKind(SyntaxKind.StringLiteralToken)
                        || token.IsKind(SyntaxKind.InterpolatedStringTextToken)
                        || token.IsKind(SyntaxKind.MultiLineRawStringLiteralToken)
                        || token.IsKind(SyntaxKind.SingleLineRawStringLiteralToken);
                    bool inAttribute = token.Parent?.AncestorsAndSelf().Any(node => node is AttributeSyntax) == true;
                    if (text && !inAttribute && RoadmapCode.IsMatch(token.ValueText))
                    {
                        offenders.Add($"{Path.GetRelativePath(src, path)}: {token.ValueText}");
                    }
                }
            }

            foreach (string path in EnumerateSourceFiles(src, "*.axaml"))
            {
                foreach (XAttribute attribute in XDocument.Load(path).Descendants().Attributes())
                {
                    if (!attribute.Value.StartsWith('{') && RoadmapCode.IsMatch(attribute.Value))
                    {
                        offenders.Add($"{Path.GetRelativePath(src, path)}: {attribute.Value}");
                    }
                }
            }

            Assert.Empty(offenders);
        }
    }
}
