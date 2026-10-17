using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using NetPrints.Roslyn;

namespace NetPrints.RepoAnalyzers.Hygiene;

/// <summary>NPH003: a diagnostic or kind code literal outside its constants class, or a code constant declared twice in one project.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RawDiagnosticCodeAnalyzer : DiagnosticAnalyzer
{
    private const string Families = "TDXWCSAHIKR";

    private const string ExperimentalPrefix = "NPXE";

    private const string ConstantsFileSuffix = "DiagnosticCodes.cs";

    private const int PrefixLength = 3;

    private const int MinimumDigits = 3;

    private const int ExperimentalCodeLength = 8;

    private static readonly ImmutableHashSet<string> DesignatedFiles = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "DocumentIssue.cs",
        "GraphCodeGenerator.cs",
        "ProjectSystemException.cs",
        "ProjectMessage.cs",
        "ExperimentalApiIds.cs",
        "ExperimentalApis.cs");

    private static readonly DiagnosticDescriptor Rule = new(
        NphDiagnosticCodes.RawDiagnosticCode,
        "Declare a diagnostic code once, in its constants class",
        "{0}",
        NphDiagnosticCodes.Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: false,
        helpLinkUri: DiagnosticHelpLinks.For(NphDiagnosticCodes.RawDiagnosticCode));

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            ConcurrentDictionary<string, bool> declared = new(StringComparer.Ordinal);
            start.RegisterSyntaxNodeAction(site => Analyze(site, declared), SyntaxKind.StringLiteralExpression);
        });
    }

    private static void Analyze(SyntaxNodeAnalysisContext context, ConcurrentDictionary<string, bool> declared)
    {
        var literal = (LiteralExpressionSyntax)context.Node;
        string code = literal.Token.ValueText;
        if (!IsCode(code))
        {
            return;
        }

        if (!IsConstantDeclaration(literal))
        {
            Report(context, literal, $"The code '{code}' is a literal; use its constant");
        }
        else if (!IsDesignated(literal.SyntaxTree.FilePath))
        {
            Report(context, literal, $"The code '{code}' is declared outside the files that declare diagnostic codes");
        }
        else if (!declared.TryAdd(code, true))
        {
            Report(context, literal, $"The code '{code}' is declared twice in this project");
        }
    }

    private static void Report(SyntaxNodeAnalysisContext context, SyntaxNode node, string message) =>
        context.ReportDiagnostic(Diagnostic.Create(Rule, node.GetLocation(), message));

    private static bool IsConstantDeclaration(LiteralExpressionSyntax literal) =>
        literal.Parent is EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax { Parent: FieldDeclarationSyntax field } } }
        && field.Modifiers.Any(SyntaxKind.ConstKeyword);

    private static bool IsDesignated(string path)
    {
        string name = path.Replace('\\', '/');
        name = name.Substring(name.LastIndexOf('/') + 1);
        return name.EndsWith(ConstantsFileSuffix, StringComparison.Ordinal) || DesignatedFiles.Contains(name);
    }

    private static bool IsCode(string text)
    {
        if (text.Length == ExperimentalCodeLength && text.StartsWith(ExperimentalPrefix, StringComparison.Ordinal))
        {
            return AllDigits(text, ExperimentalPrefix.Length);
        }

        return text.Length > PrefixLength + MinimumDigits - 1
            && text.StartsWith("NP", StringComparison.Ordinal)
            && Families.IndexOf(text[PrefixLength - 1]) >= 0
            && AllDigits(text, PrefixLength);
    }

    private static bool AllDigits(string text, int start)
    {
        for (int index = start; index < text.Length; index++)
        {
            if (text[index] < '0' || text[index] > '9')
            {
                return false;
            }
        }

        return true;
    }
}
