using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NetPrints.RepoAnalyzers.Style;

/// <summary>The syntax nodes that hold members, and how their members are read.</summary>
internal static class MemberContainers
{
    /// <summary>The kinds of node that hold a list of members.</summary>
    public static readonly SyntaxKind[] Kinds =
    [
        SyntaxKind.CompilationUnit,
        SyntaxKind.NamespaceDeclaration,
        SyntaxKind.FileScopedNamespaceDeclaration,
        SyntaxKind.ClassDeclaration,
        SyntaxKind.StructDeclaration,
        SyntaxKind.InterfaceDeclaration,
        SyntaxKind.RecordDeclaration,
        SyntaxKind.RecordStructDeclaration,
    ];

    /// <summary>Reads the members of a container node.</summary>
    /// <param name="container">A node of one of <see cref="Kinds"/>.</param>
    /// <returns>Its members, or an empty list for any other node.</returns>
    public static SyntaxList<MemberDeclarationSyntax> Members(SyntaxNode container) =>
        container switch
        {
            CompilationUnitSyntax unit => unit.Members,
            BaseNamespaceDeclarationSyntax space => space.Members,
            TypeDeclarationSyntax type => type.Members,
            _ => default,
        };
}
