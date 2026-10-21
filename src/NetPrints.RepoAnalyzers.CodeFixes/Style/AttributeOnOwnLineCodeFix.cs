using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace NetPrints.RepoAnalyzers.Style;

/// <summary>Fix for NPS001: breaks the line after the attribute list and indents the next token like the list.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AttributeOnOwnLineCodeFix))]
[Shared]
public sealed class AttributeOnOwnLineCodeFix : CodeFixProvider
{
    private const string Title = "Put the attribute on its own line";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => [NpsDiagnosticCodes.AttributeOnOwnLine];

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        SourceText text = await context.Document.GetTextAsync(context.CancellationToken).ConfigureAwait(false);
        Diagnostic diagnostic = context.Diagnostics[0];
        AttributeListSyntax? list = root?.FindNode(diagnostic.Location.SourceSpan) as AttributeListSyntax;
        SyntaxToken next = list?.GetLastToken().GetNextToken() ?? default;
        if (list is null || next.RawKind == 0)
        {
            return;
        }

        TextSpan gap = TextSpan.FromBounds(list.Span.End, next.SpanStart);
        if (text.ToString(gap).Trim().Length != 0)
        {
            return;
        }

        string replacement = LineBreaks.For(text) + LineBreaks.IndentOf(text, list.SpanStart);
        context.RegisterCodeFix(
            CodeAction.Create(Title, (CancellationToken cancellationToken) => ReplaceAsync(context.Document, text, gap, replacement), Title),
            diagnostic);
    }

    private static Task<Document> ReplaceAsync(Document document, SourceText text, TextSpan gap, string replacement) =>
        Task.FromResult(document.WithText(text.WithChanges(new TextChange(gap, replacement))));
}
