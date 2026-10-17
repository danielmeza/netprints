using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using NetPrints.Roslyn;

namespace NetPrints.RepoAnalyzers.Hygiene;

/// <summary>NPH002: a type or delegate name ends in <c>VM</c> or has <c>VM</c> before an upper-case letter; the name is <c>ViewModel</c>.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ViewModelSuffixAnalyzer : DiagnosticAnalyzer
{
    private const string Abbreviation = "VM";

    private const string Allowed = "MVVM";

    private static readonly SyntaxKind[] Kinds =
    [
        SyntaxKind.ClassDeclaration,
        SyntaxKind.StructDeclaration,
        SyntaxKind.InterfaceDeclaration,
        SyntaxKind.RecordDeclaration,
        SyntaxKind.RecordStructDeclaration,
        SyntaxKind.EnumDeclaration,
        SyntaxKind.DelegateDeclaration,
    ];

    private static readonly DiagnosticDescriptor Rule = new(
        NphDiagnosticCodes.ViewModelSuffix,
        "Name a view model ViewModel, not VM",
        "The type name '{0}' abbreviates ViewModel as VM",
        NphDiagnosticCodes.Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: false,
        helpLinkUri: DiagnosticHelpLinks.For(NphDiagnosticCodes.ViewModelSuffix));

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, Kinds);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        SyntaxToken identifier = context.Node is DelegateDeclarationSyntax declaredDelegate
            ? declaredDelegate.Identifier
            : ((BaseTypeDeclarationSyntax)context.Node).Identifier;
        string name = identifier.ValueText;
        if (Abbreviates(name))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, identifier.GetLocation(), name));
        }
    }

    private static bool Abbreviates(string name)
    {
        string text = name.Replace(Allowed, string.Empty);
        if (text.EndsWith(Abbreviation, StringComparison.Ordinal))
        {
            return true;
        }

        int index = text.IndexOf(Abbreviation, StringComparison.Ordinal);
        while (index >= 0)
        {
            int next = index + Abbreviation.Length;
            if (next < text.Length && text[next] >= 'A' && text[next] <= 'Z')
            {
                return true;
            }

            index = text.IndexOf(Abbreviation, next, StringComparison.Ordinal);
        }

        return false;
    }
}
