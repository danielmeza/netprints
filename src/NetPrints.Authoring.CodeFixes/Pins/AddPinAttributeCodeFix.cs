using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NetPrints.Authoring.Pins;

/// <summary>Fix for NPA008: adds <c>[Pin("Name")]</c> with the property name as the key, which is the key the convention used so far.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AddPinAttributeCodeFix))]
[Shared]
public sealed class AddPinAttributeCodeFix : CodeFixProvider
{
    private const string Title = "Add [Pin]";

    private const string PinAttributeName = "Pin";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => [NpaDiagnosticCodes.NameWithoutSuffix];

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        Diagnostic diagnostic = context.Diagnostics[0];
        if (root?.FindNode(diagnostic.Location.SourceSpan) is not PropertyDeclarationSyntax property)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(Title, (CancellationToken cancellationToken) => Task.FromResult(Apply(context.Document, root, property)), Title),
            diagnostic);
    }

    private static Document Apply(Document document, SyntaxNode root, PropertyDeclarationSyntax property) =>
        document.WithSyntaxRoot(root.ReplaceNode(property, NpaSyntax.PrependAttribute(property, root, PinAttributeName, property.Identifier.Text)));
}
