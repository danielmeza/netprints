using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using NetPrints.Roslyn;

namespace NetPrints.RepoAnalyzers.Style;

/// <summary>
/// NPS002 and NPS003: adjacent members need a blank line between them. Non-public fields and consts that sit on one
/// line each, with no comment, attribute or braces, may stay grouped; NPS003 is the suggestion that points at them.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class BlankLineBetweenMembersAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor MissingBlankLine = new(
        NpsDiagnosticCodes.BlankLineBetweenMembers,
        "Separate members with a blank line",
        "Insert a blank line before this member",
        NpsDiagnosticCodes.Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: false,
        helpLinkUri: DiagnosticHelpLinks.For(NpsDiagnosticCodes.BlankLineBetweenMembers));

    private static readonly DiagnosticDescriptor GroupedFields = new(
        NpsDiagnosticCodes.GroupedFields,
        "Separate grouped fields with a blank line",
        "Consider a blank line before this field",
        NpsDiagnosticCodes.Category,
        DiagnosticSeverity.Info,
        isEnabledByDefault: false,
        helpLinkUri: DiagnosticHelpLinks.For(NpsDiagnosticCodes.GroupedFields));

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [MissingBlankLine, GroupedFields];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeContainer, MemberContainers.Kinds);
    }

    private static void AnalyzeContainer(SyntaxNodeAnalysisContext context)
    {
        SyntaxList<MemberDeclarationSyntax> members = MemberContainers.Members(context.Node);
        if (members.Count < 2)
        {
            return;
        }

        SourceText text = context.Node.SyntaxTree.GetText(context.CancellationToken);
        for (int index = 1; index < members.Count; index++)
        {
            MemberDeclarationSyntax previous = members[index - 1];
            MemberDeclarationSyntax current = members[index];
            if (previous is GlobalStatementSyntax || current is GlobalStatementSyntax || HasBlankLineBetween(text, previous, current))
            {
                continue;
            }

            DiagnosticDescriptor rule = IsGroupable(text, previous) && IsGroupable(text, current) ? GroupedFields : MissingBlankLine;
            context.ReportDiagnostic(Diagnostic.Create(rule, current.GetFirstToken().GetLocation()));
        }
    }

    private static bool HasBlankLineBetween(SourceText text, MemberDeclarationSyntax previous, MemberDeclarationSyntax current)
    {
        int previousLine = text.Lines.GetLineFromPosition(previous.Span.End).LineNumber;
        SyntaxTrivia content = current.GetLeadingTrivia().FirstOrDefault(trivia => !trivia.IsKind(SyntaxKind.WhitespaceTrivia) && !trivia.IsKind(SyntaxKind.EndOfLineTrivia));
        int contentStart = content.IsKind(SyntaxKind.None) ? current.SpanStart : content.SpanStart;
        int currentLine = text.Lines.GetLineFromPosition(contentStart).LineNumber;

        for (int line = previousLine + 1; line < currentLine; line++)
        {
            if (string.IsNullOrWhiteSpace(text.Lines[line].ToString()))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsGroupable(SourceText text, MemberDeclarationSyntax member)
    {
        if (member is not FieldDeclarationSyntax field
            || field.Parent is InterfaceDeclarationSyntax
            || field.AttributeLists.Count > 0
            || field.Modifiers.Any(SyntaxKind.PublicKeyword)
            || field.DescendantTokens().Any(token => token.IsKind(SyntaxKind.OpenBraceToken))
            || field.GetLeadingTrivia().Concat(field.GetLastToken().TrailingTrivia).Any(IsComment))
        {
            return false;
        }

        return text.Lines.GetLineFromPosition(field.SpanStart).LineNumber == text.Lines.GetLineFromPosition(field.Span.End).LineNumber;
    }

    private static bool IsComment(SyntaxTrivia trivia) =>
        trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
        || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
        || trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
        || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia);
}
