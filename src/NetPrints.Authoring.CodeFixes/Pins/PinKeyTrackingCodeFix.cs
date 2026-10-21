using System;
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

namespace NetPrints.Authoring.Pins;

/// <summary>Fixes for NPA010 (record the key in the unshipped file) and NPA011 (keep the shipped key, or remove it on purpose).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PinKeyTrackingCodeFix))]
[Shared]
public sealed class PinKeyTrackingCodeFix : CodeFixProvider
{
    private const string RecordTitle = "Record the pin key in NodePins.Unshipped.txt";

    private const string KeepTitle = "Keep the shipped pin key";

    private const string RemoveTitle = "Remove the shipped pin key on purpose";

    private const string UnshippedFile = "NodePins.Unshipped.txt";

    private const string RemovedPrefix = "*REMOVED*";

    private const string LineProperty = "Line";

    private const string OldKeyProperty = "OldKey";

    private const string PinAttributeName = "Pin";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => [NpaDiagnosticCodes.UnrecordedKey, NpaDiagnosticCodes.ShippedKeyGone];

    /// <inheritdoc/>
    public override FixAllProvider? GetFixAllProvider() => null;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        Diagnostic diagnostic = context.Diagnostics[0];
        if (!diagnostic.Properties.TryGetValue(LineProperty, out string? line) || line is null)
        {
            return;
        }

        TextDocument? unshipped = context.Document.Project.AdditionalDocuments.FirstOrDefault(item => string.Equals(item.Name, UnshippedFile, StringComparison.Ordinal));
        if (unshipped is null)
        {
            return;
        }

        if (diagnostic.Id == NpaDiagnosticCodes.UnrecordedKey)
        {
            context.RegisterCodeFix(
                CodeAction.Create(RecordTitle, cancellationToken => AppendAsync(context.Document.Project.Solution, unshipped, line, cancellationToken), RecordTitle),
                diagnostic);
            return;
        }

        SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root?.FindNode(diagnostic.Location.SourceSpan) is PropertyDeclarationSyntax property
            && diagnostic.Properties.TryGetValue(OldKeyProperty, out string? oldKey)
            && oldKey is not null)
        {
            context.RegisterCodeFix(
                CodeAction.Create(KeepTitle, cancellationToken => Task.FromResult(KeepKey(context.Document, root, property, oldKey)), KeepTitle),
                diagnostic);
        }

        context.RegisterCodeFix(
            CodeAction.Create(RemoveTitle, cancellationToken => AppendAsync(context.Document.Project.Solution, unshipped, RemovedPrefix + line, cancellationToken), RemoveTitle),
            diagnostic);
    }

    private static async Task<Solution> AppendAsync(Solution solution, TextDocument unshipped, string line, CancellationToken cancellationToken)
    {
        string text = (await unshipped.GetTextAsync(cancellationToken).ConfigureAwait(false)).ToString();
        string separator = text.Length == 0 || text.EndsWith("\n", StringComparison.Ordinal) ? string.Empty : "\n";
        return solution.WithAdditionalDocumentText(unshipped.Id, SourceText.From(text + separator + line + "\n"));
    }

    private static Solution KeepKey(Document document, SyntaxNode root, PropertyDeclarationSyntax property, string oldKey)
    {
        AttributeSyntax? existing = property.AttributeLists.SelectMany(list => list.Attributes).FirstOrDefault(IsPin);
        SyntaxNode newRoot;
        if (existing is null)
        {
            newRoot = root.ReplaceNode(property, NpaSyntax.PrependAttribute(property, root, PinAttributeName, oldKey));
        }
        else
        {
            AttributeArgumentSyntax key = SyntaxFactory.AttributeArgument(SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(oldKey)));
            SeparatedSyntaxList<AttributeArgumentSyntax> named = SyntaxFactory.SeparatedList(existing.ArgumentList?.Arguments.Where(argument => argument.NameEquals is not null) ?? []);
            AttributeArgumentListSyntax arguments = SyntaxFactory.AttributeArgumentList(named.Insert(0, key));
            newRoot = root.ReplaceNode(existing, existing.WithArgumentList(arguments));
        }

        return document.WithSyntaxRoot(newRoot).Project.Solution;
    }

    private static bool IsPin(AttributeSyntax attribute)
    {
        string name = attribute.Name is QualifiedNameSyntax qualified ? qualified.Right.Identifier.Text : attribute.Name.ToString();
        return name == PinAttributeName || name == PinAttributeName + "Attribute";
    }
}
