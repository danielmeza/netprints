using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace NetPrints.RepoAnalyzers.Style;

/// <summary>Fix for NPS002 and NPS003: inserts a blank line above the reported member, above its leading comments.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(BlankLineBetweenMembersCodeFix))]
[Shared]
public sealed class BlankLineBetweenMembersCodeFix : CodeFixProvider
{
    private const string Title = "Insert a blank line";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => [NpsDiagnosticCodes.BlankLineBetweenMembers, NpsDiagnosticCodes.GroupedFields];

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        SourceText text = await context.Document.GetTextAsync(context.CancellationToken).ConfigureAwait(false);
        Diagnostic diagnostic = context.Diagnostics[0];
        SyntaxToken token = root?.FindToken(diagnostic.Location.SourceSpan.Start) ?? default;
        MemberDeclarationSyntax? member = token.Parent?.AncestorsAndSelf().OfType<MemberDeclarationSyntax>().FirstOrDefault(candidate => candidate.GetFirstToken() == token);
        if (member is null)
        {
            return;
        }

        TextChange change = ChangeFor(text, member);
        context.RegisterCodeFix(
            CodeAction.Create(Title, (CancellationToken cancellationToken) => Task.FromResult(context.Document.WithText(text.WithChanges(change))), Title),
            diagnostic);
    }

    private static TextChange ChangeFor(SourceText text, MemberDeclarationSyntax member)
    {
        int start = member.FullSpan.Start;
        string lineBreak = LineBreaks.For(text);
        if (text.Lines.GetLineFromPosition(start).Start == start)
        {
            return new TextChange(new TextSpan(start, 0), lineBreak);
        }

        int begin = start;
        while (begin > 0 && (text[begin - 1] == ' ' || text[begin - 1] == '\t'))
        {
            begin--;
        }

        int end = start;
        while (end < text.Length && (text[end] == ' ' || text[end] == '\t'))
        {
            end++;
        }

        return new TextChange(TextSpan.FromBounds(begin, end), lineBreak + lineBreak + LineBreaks.IndentOf(text, start));
    }
}
