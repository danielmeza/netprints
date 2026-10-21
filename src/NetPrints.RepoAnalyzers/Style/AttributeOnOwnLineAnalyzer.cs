using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using NetPrints.Roslyn;

namespace NetPrints.RepoAnalyzers.Style;

/// <summary>NPS001: an attribute list on a member shares a line with the next attribute list or with the member itself.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AttributeOnOwnLineAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        NpsDiagnosticCodes.AttributeOnOwnLine,
        "Put each attribute list on its own line",
        "Put the attribute list on its own line, above the member",
        NpsDiagnosticCodes.Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: false,
        helpLinkUri: DiagnosticHelpLinks.For(NpsDiagnosticCodes.AttributeOnOwnLine));

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeContainer, MemberContainers.Kinds);
    }

    private static void AnalyzeContainer(SyntaxNodeAnalysisContext context)
    {
        foreach (AttributeListSyntax list in MemberContainers.Members(context.Node).SelectMany(member => member.AttributeLists))
        {
            if (!list.CloseBracketToken.TrailingTrivia.Any(SyntaxKind.EndOfLineTrivia))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, list.GetLocation()));
            }
        }
    }
}
