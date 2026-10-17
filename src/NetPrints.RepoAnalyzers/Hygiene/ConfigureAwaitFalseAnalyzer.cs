using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using NetPrints.Roslyn;

namespace NetPrints.RepoAnalyzers.Hygiene;

/// <summary>NPH005: <c>ConfigureAwait(false)</c> in UI-bound code, the files where <c>.editorconfig</c> sets <c>VSTHRD111</c> to <c>none</c>.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ConfigureAwaitFalseAnalyzer : DiagnosticAnalyzer
{
    private const string MethodName = "ConfigureAwait";

    private const string SeverityKey = "dotnet_diagnostic.VSTHRD111.severity";

    private const string UiBoundSeverity = "none";

    private static readonly DiagnosticDescriptor Rule = new(
        NphDiagnosticCodes.ConfigureAwaitFalse,
        "UI-bound code resumes on the UI thread",
        "ConfigureAwait(false) in UI-bound code resumes off the UI thread",
        NphDiagnosticCodes.Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: false,
        helpLinkUri: DiagnosticHelpLinks.For(NphDiagnosticCodes.ConfigureAwaitFalse));

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var call = (InvocationExpressionSyntax)context.Node;
        if (call.Expression is not MemberAccessExpressionSyntax { Name.Identifier.Text: MethodName } access
            || call.ArgumentList.Arguments.Count != 1
            || !call.ArgumentList.Arguments[0].Expression.IsKind(SyntaxKind.FalseLiteralExpression))
        {
            return;
        }

        AnalyzerConfigOptions options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(call.SyntaxTree);
        if (options.TryGetValue(SeverityKey, out string? severity) && severity == UiBoundSeverity)
        {
            Location location = Location.Create(call.SyntaxTree, TextSpan.FromBounds(access.Name.SpanStart, call.Span.End));
            context.ReportDiagnostic(Diagnostic.Create(Rule, location));
        }
    }
}
