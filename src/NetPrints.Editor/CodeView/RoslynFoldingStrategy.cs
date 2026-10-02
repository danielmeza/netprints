using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NetPrints.Editor.CodeView;

/// <summary>
/// One collapsible region of the code view: a type or member body (editor-services.md §3).
/// </summary>
/// <param name="Start">Character offset where the collapsed region starts (right after the opening brace).</param>
/// <param name="End">Character offset where the collapsed region ends (right before the closing brace).</param>
/// <param name="Title">Placeholder text shown while the region is collapsed.</param>
public sealed record FoldingRange(int Start, int End, string Title);

/// <summary>
/// Computes <see cref="FoldingRange"/>s for a class's generated C# from its Roslyn syntax tree
/// (research.md R2: "AvaloniaEdit has no C# folding strategy"), for type declarations and member
/// bodies (constructors, methods, accessors). A parse-only walk (no semantic model), so it is cheap
/// enough to run on the UI thread; UI-toolkit-free, so <c>CodeViewViewModel</c> can call it directly (FR-038).
/// </summary>
public static class RoslynFoldingStrategy
{
    /// <summary>Placeholder shown for a collapsed region, regardless of what it is.</summary>
    public const string CollapsedPlaceholder = "{ … }";

    /// <summary>
    /// Parses <paramref name="code"/> and returns a folding for every type declaration's body and
    /// every member body with a block (an expression-bodied member has no braces to fold), sorted by
    /// <see cref="FoldingRange.Start"/>.
    /// </summary>
    /// <param name="code">Generated C# to compute foldings for.</param>
    /// <returns>The class's foldings, outermost and innermost alike, in document order.</returns>
    public static IReadOnlyList<FoldingRange> ComputeFoldings(string code)
    {
        ArgumentNullException.ThrowIfNull(code);

        var foldings = new List<FoldingRange>();
        SyntaxNode root = CSharpSyntaxTree.ParseText(code).GetRoot();
        foreach (SyntaxNode node in root.DescendantNodes())
        {
            switch (node)
            {
                case BaseTypeDeclarationSyntax type:
                    AddFolding(foldings, type.OpenBraceToken, type.CloseBraceToken);
                    break;
                case BaseMethodDeclarationSyntax { Body: { } body }:
                    AddFolding(foldings, body.OpenBraceToken, body.CloseBraceToken);
                    break;
                case AccessorDeclarationSyntax { Body: { } accessorBody }:
                    AddFolding(foldings, accessorBody.OpenBraceToken, accessorBody.CloseBraceToken);
                    break;
            }
        }

        foldings.Sort((left, right) => left.Start.CompareTo(right.Start));
        return foldings;
    }

    private static void AddFolding(List<FoldingRange> foldings, SyntaxToken openBrace, SyntaxToken closeBrace)
    {
        if (openBrace.IsMissing || closeBrace.IsMissing)
        {
            return;
        }

        int start = openBrace.Span.End;
        int end = closeBrace.Span.Start;
        if (end > start)
        {
            foldings.Add(new FoldingRange(start, end, CollapsedPlaceholder));
        }
    }
}
