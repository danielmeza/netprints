using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using NetPrints.Roslyn;

namespace NetPrints.RepoAnalyzers.Hygiene;

/// <summary>NPH001: the null-forgiving operator, <c>x!</c>, <c>null!</c> or <c>default!</c>, at a site the ledger does not list.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NullForgivingAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        NphDiagnosticCodes.NullForgiving,
        "Do not use the null-forgiving operator",
        "Model the null instead of silencing it: '{0}'",
        NphDiagnosticCodes.Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: false,
        helpLinkUri: DiagnosticHelpLinks.For(NphDiagnosticCodes.NullForgiving));

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            HygieneLedger ledger = HygieneLedger.Read(start.Options.AdditionalFiles, start.CancellationToken);
            start.RegisterSyntaxNodeAction(site => Analyze(site, ledger), SyntaxKind.SuppressNullableWarningExpression);
        });
    }

    private static void Analyze(SyntaxNodeAnalysisContext context, HygieneLedger ledger)
    {
        SyntaxTree tree = context.Node.SyntaxTree;
        int line = context.Node.GetLocation().GetLineSpan().StartLinePosition.Line;
        string lineText = tree.GetText(context.CancellationToken).Lines[line].ToString().Trim();
        if (!ledger.Contains(NphDiagnosticCodes.NullForgiving, tree.FilePath, lineText))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, context.Node.GetLocation(), lineText));
        }
    }
}
