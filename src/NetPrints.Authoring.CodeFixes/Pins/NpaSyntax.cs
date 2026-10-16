using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NetPrints.Authoring.Pins;

/// <summary>Syntax helpers shared by the NPA code fixes.</summary>
internal static class NpaSyntax
{
    private const string GraphNamespace = "NetPrints.Graph";

    private const string CarriageReturnLineFeed = "\r\n";

    private const string LineFeed = "\n";

    public static T WithPartial<T>(T declaration)
        where T : MemberDeclarationSyntax
    {
        SyntaxTokenList modifiers = declaration.Modifiers;
        if (modifiers.Any(SyntaxKind.PartialKeyword))
        {
            return declaration;
        }

        SyntaxToken partial = SyntaxFactory.Token(SyntaxKind.PartialKeyword).WithTrailingTrivia(SyntaxFactory.Space);
        if (modifiers.Count > 0)
        {
            return (T)declaration.WithModifiers(modifiers.Add(partial));
        }

        SyntaxToken first = declaration.GetFirstToken();
        SyntaxToken stripped = first.WithLeadingTrivia(SyntaxFactory.TriviaList());
        return (T)declaration.ReplaceToken(first, stripped).WithModifiers(SyntaxFactory.TokenList(partial.WithLeadingTrivia(first.LeadingTrivia)));
    }

    public static T PrependAttribute<T>(T member, SyntaxNode root, string name, string? argument)
        where T : MemberDeclarationSyntax
    {
        SyntaxTriviaList leading = member.GetLeadingTrivia();
        SyntaxTriviaList indent = leading.Count > 0 && leading[leading.Count - 1].IsKind(SyntaxKind.WhitespaceTrivia)
            ? SyntaxFactory.TriviaList(leading[leading.Count - 1])
            : SyntaxFactory.TriviaList();

        string qualified = HasGraphUsing(root) ? name : GraphNamespace + "." + name;
        AttributeSyntax attribute = SyntaxFactory.Attribute(SyntaxFactory.ParseName(qualified));
        if (argument is not null)
        {
            attribute = attribute.WithArgumentList(SyntaxFactory.AttributeArgumentList(SyntaxFactory.SingletonSeparatedList(
                SyntaxFactory.AttributeArgument(SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(argument))))));
        }

        string newLine = root.ToFullString().Contains(CarriageReturnLineFeed) ? CarriageReturnLineFeed : LineFeed;
        AttributeListSyntax list = SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(attribute))
            .WithLeadingTrivia(leading)
            .WithTrailingTrivia(SyntaxFactory.EndOfLine(newLine));

        T stripped = member.WithLeadingTrivia(indent);
        return (T)stripped.WithAttributeLists(stripped.AttributeLists.Insert(0, list));
    }

    private static bool HasGraphUsing(SyntaxNode root) =>
        root.DescendantNodes().OfType<UsingDirectiveSyntax>().Any(directive => directive.Name?.ToString() == GraphNamespace);
}
