using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using NetPrints.Roslyn;

namespace NetPrints.RepoAnalyzers.Hygiene;

/// <summary>NPH004: a pragma, a nullable escape or a <c>[SuppressMessage]</c> that the ADR-0003 ledger does not list.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnlistedSuppressionAnalyzer : DiagnosticAnalyzer
{
    private const string AttributeSuffix = "Attribute";

    private const string SuppressMessageName = "SuppressMessage";

    private const string JustificationMarker = "ADR-" + "0003";

    private static readonly DiagnosticDescriptor Rule = new(
        NphDiagnosticCodes.UnlistedSuppression,
        "Fix the diagnostic instead of suppressing it",
        "{0}",
        NphDiagnosticCodes.Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: false,
        helpLinkUri: DiagnosticHelpLinks.For(NphDiagnosticCodes.UnlistedSuppression));

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxTreeAction(AnalyzeDirectives);
        context.RegisterCompilationStartAction(start =>
        {
            HygieneLedger ledger = HygieneLedger.Read(start.Options.AdditionalFiles, start.CancellationToken);
            start.RegisterSyntaxNodeAction(site => AnalyzeAttribute(site, ledger), SyntaxKind.Attribute);
        });
    }

    private static void AnalyzeDirectives(SyntaxTreeAnalysisContext context)
    {
        SyntaxNode root = context.Tree.GetRoot(context.CancellationToken);
        foreach (SyntaxTrivia trivia in root.DescendantTrivia())
        {
            if (!trivia.IsDirective || trivia.GetStructure() is not DirectiveTriviaSyntax directive)
            {
                continue;
            }

            bool escapes = directive is PragmaWarningDirectiveTriviaSyntax
                || directive is NullableDirectiveTriviaSyntax { SettingToken: var setting } && (setting.IsKind(SyntaxKind.DisableKeyword) || setting.IsKind(SyntaxKind.RestoreKeyword));
            if (escapes)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, directive.GetLocation(), "A directive suppresses diagnostics; fix the cause or list the site in the ledger"));
            }
        }
    }

    private static void AnalyzeAttribute(SyntaxNodeAnalysisContext context, HygieneLedger ledger)
    {
        var attribute = (AttributeSyntax)context.Node;
        if (!IsSuppressMessage(attribute))
        {
            return;
        }

        SeparatedSyntaxList<AttributeArgumentSyntax> arguments = attribute.ArgumentList?.Arguments ?? default;
        string? rule = arguments.Count > 1 ? arguments[1].Expression.ToString().Trim('"') : null;
        string? member = ContainingMemberName(attribute);
        if (rule is null || member is null || !ledger.Contains(NphDiagnosticCodes.UnlistedSuppression, attribute.SyntaxTree.FilePath, member + ":" + rule))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, attribute.GetLocation(), $"The suppression of {rule ?? "?"} on {member ?? "?"} is not in the ledger"));
        }
        else if (!attribute.ToString().Contains(JustificationMarker))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, attribute.GetLocation(), $"The suppression of {rule} has no {JustificationMarker} justification"));
        }
    }

    private static bool IsSuppressMessage(AttributeSyntax attribute)
    {
        string name = attribute.Name.ToString();
        if (name.EndsWith(AttributeSuffix, StringComparison.Ordinal))
        {
            name = name.Substring(0, name.Length - AttributeSuffix.Length);
        }

        return name.EndsWith(SuppressMessageName, StringComparison.Ordinal);
    }

    private static string? ContainingMemberName(SyntaxNode attribute)
    {
        for (SyntaxNode? node = attribute.Parent; node is not null; node = node.Parent)
        {
            if (node is MethodDeclarationSyntax method)
            {
                return method.Identifier.Text;
            }

            if (node is PropertyDeclarationSyntax property)
            {
                return property.Identifier.Text;
            }

            if (node is ConstructorDeclarationSyntax constructor)
            {
                return constructor.Identifier.Text;
            }
        }

        return null;
    }
}
