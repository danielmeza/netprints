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

namespace NetPrints.Authoring.Pins;

/// <summary>Fix for NPA001: makes the class and its containing types partial, or turns the property into a partial get-only property.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MakePartialCodeFix))]
[Shared]
public sealed class MakePartialCodeFix : CodeFixProvider
{
    private const string Title = "Make partial";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => [NpaDiagnosticCodes.PinShape];

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        Diagnostic diagnostic = context.Diagnostics[0];
        SyntaxNode? node = root?.FindNode(diagnostic.Location.SourceSpan);
        if (root is null || node is not (ClassDeclarationSyntax or PropertyDeclarationSyntax))
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(Title, (CancellationToken cancellationToken) => Task.FromResult(context.Document.WithSyntaxRoot(Fix(root, node))), Title),
            diagnostic);
    }

    private static SyntaxNode Fix(SyntaxNode root, SyntaxNode node) =>
        node is PropertyDeclarationSyntax property ? root.ReplaceNode(property, MakePartialGetOnly(property)) : MakeTypesPartial(root, node);

    private static SyntaxNode MakeTypesPartial(SyntaxNode root, SyntaxNode node)
    {
        TypeDeclarationSyntax[] types = [.. node.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().Where(type => !type.Modifiers.Any(SyntaxKind.PartialKeyword))];
        return root.ReplaceNodes(types, (original, _) => NpaSyntax.WithPartial(original));
    }

    private static PropertyDeclarationSyntax MakePartialGetOnly(PropertyDeclarationSyntax property)
    {
        SyntaxTriviaList closing = property.GetLastToken().TrailingTrivia;
        AccessorDeclarationSyntax getter = SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken).WithTrailingTrivia(SyntaxFactory.Space));
        AccessorListSyntax accessors = SyntaxFactory.AccessorList(SyntaxFactory.SingletonList(getter))
            .WithOpenBraceToken(SyntaxFactory.Token(SyntaxKind.OpenBraceToken).WithTrailingTrivia(SyntaxFactory.Space))
            .WithCloseBraceToken(SyntaxFactory.Token(SyntaxKind.CloseBraceToken).WithTrailingTrivia(closing));

        PropertyDeclarationSyntax rewritten = property
            .WithExpressionBody(null)
            .WithInitializer(null)
            .WithSemicolonToken(default)
            .WithAccessorList(accessors);
        return NpaSyntax.WithPartial(rewritten);
    }
}
