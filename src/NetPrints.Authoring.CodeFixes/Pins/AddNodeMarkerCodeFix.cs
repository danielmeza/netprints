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

/// <summary>Fix for NPA009: adds <c>[NodeBase]</c> to an abstract class, or <c>[NodeKind("ClassName")]</c> to a concrete one for the author to rename.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AddNodeMarkerCodeFix))]
[Shared]
public sealed class AddNodeMarkerCodeFix : CodeFixProvider
{
    private const string Title = "Add the node marker";

    private const string NodeBaseName = "NodeBase";

    private const string NodeKindName = "NodeKind";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => [NpaDiagnosticCodes.MissingMarker];

    /// <inheritdoc/>
    public override FixAllProvider? GetFixAllProvider() => null;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        Diagnostic diagnostic = context.Diagnostics[0];
        ClassDeclarationSyntax? type = root?.FindNode(diagnostic.Location.SourceSpan).AncestorsAndSelf().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        if (root is null || type is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(Title, (CancellationToken cancellationToken) => Task.FromResult(Apply(context.Document, root, type)), Title),
            diagnostic);
    }

    private static Document Apply(Document document, SyntaxNode root, ClassDeclarationSyntax type)
    {
        ClassDeclarationSyntax marked = type.Modifiers.Any(SyntaxKind.AbstractKeyword)
            ? NpaSyntax.PrependAttribute(type, root, NodeBaseName, null)
            : NpaSyntax.PrependAttribute(type, root, NodeKindName, type.Identifier.Text);
        return document.WithSyntaxRoot(root.ReplaceNode(type, marked));
    }
}
