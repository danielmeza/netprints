using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NetPrints.RepoAnalyzers.Composition;
using NetPrints.RepoAnalyzers.Generation;
using NetPrints.Roslyn;

namespace NetPrints.RepoAnalyzers.SmartEnums;

/// <summary>A type marked <c>[SmartEnum]</c>, as the pipeline holds it.</summary>
/// <param name="Namespace">The containing namespace, or an empty string.</param>
/// <param name="Name">The class name.</param>
/// <param name="Accessibility">The accessibility keyword of the class, shared by the companion enum.</param>
/// <param name="Members">The member field names in declaration order; empty when the shape is invalid.</param>
/// <param name="Diagnostics">The problems found while reading it.</param>
internal sealed record SmartEnumModel(string Namespace, string Name, string Accessibility, EquatableArray<string> Members, EquatableArray<DiagnosticModel> Diagnostics);

/// <summary>Reads smart enum classes into pipeline models.</summary>
internal static class SmartEnumReader
{
    private const string AllName = "All";

    public static SmartEnumModel Read(GeneratorAttributeSyntaxContext context, CancellationToken token)
    {
        INamedTypeSymbol type = (INamedTypeSymbol)context.TargetSymbol;
        string ns = type.ContainingNamespace.IsGlobalNamespace ? string.Empty : type.ContainingNamespace.ToDisplayString();
        string accessibility = type.DeclaredAccessibility == Accessibility.Public ? "public" : "internal";
        List<DiagnosticModel> diagnostics = [];

        if (!HasShape(type, token))
        {
            LocationInfo location = LocationInfo.From(context.Attributes.FirstOrDefault()?.ApplicationSyntaxReference?.GetSyntax(token).GetLocation());
            diagnostics.Add(new DiagnosticModel(SmartEnumDiagnosticCodes.Shape, location, type.Name, string.Empty));
            return new SmartEnumModel(ns, type.Name, accessibility, default, new EquatableArray<DiagnosticModel>(diagnostics.ToArray()));
        }

        IFieldSymbol[] members = [.. type.GetMembers().OfType<IFieldSymbol>().Where(field => IsMember(field, type) && field.IsReadOnly)];
        diagnostics.AddRange(NotReadonlyMembers(type));
        diagnostics.AddRange(EarlyLookups(type, members, token));
        return new SmartEnumModel(ns, type.Name, accessibility, new EquatableArray<string>([.. members.Select(member => member.Name)]), new EquatableArray<DiagnosticModel>(diagnostics.ToArray()));
    }

    private static bool HasShape(INamedTypeSymbol type, CancellationToken token) =>
        type is { TypeKind: TypeKind.Class, IsAbstract: true, IsStatic: false, ContainingType: null }
        && type.DeclaringSyntaxReferences.Any(reference => reference.GetSyntax(token) is ClassDeclarationSyntax syntax && syntax.Modifiers.Any(SyntaxKind.PartialKeyword));

    private static bool IsMember(IFieldSymbol field, INamedTypeSymbol type) =>
        field is { IsStatic: true, IsConst: false, IsImplicitlyDeclared: false, DeclaredAccessibility: not Accessibility.Private }
        && SymbolEqualityComparer.Default.Equals(field.Type, type);

    private static IEnumerable<DiagnosticModel> NotReadonlyMembers(INamedTypeSymbol type)
    {
        foreach (ISymbol member in type.GetMembers())
        {
            bool notReadonlyField = member is IFieldSymbol field && IsMember(field, type) && !field.IsReadOnly;
            bool property = member is IPropertySymbol { IsStatic: true, DeclaredAccessibility: not Accessibility.Private } candidate && SymbolEqualityComparer.Default.Equals(candidate.Type, type);
            if (notReadonlyField || property)
            {
                yield return new DiagnosticModel(SmartEnumDiagnosticCodes.Member, LocationInfo.From(member.Locations.FirstOrDefault()), member.Name, "a member must be a static readonly field");
            }
        }
    }

    private static IEnumerable<DiagnosticModel> EarlyLookups(INamedTypeSymbol type, IFieldSymbol[] members, CancellationToken token)
    {
        foreach (SyntaxReference reference in type.DeclaringSyntaxReferences)
        {
            foreach (FieldDeclarationSyntax declaration in reference.GetSyntax(token).DescendantNodes().OfType<FieldDeclarationSyntax>().Where(field => field.Modifiers.Any(SyntaxKind.StaticKeyword)))
            {
                foreach (IdentifierNameSyntax identifier in declaration.Declaration.Variables.Select(variable => variable.Initializer).OfType<EqualsValueClauseSyntax>().SelectMany(initializer => initializer.DescendantNodes().OfType<IdentifierNameSyntax>()).Where(name => name.Identifier.ValueText == AllName))
                {
                    if (members.Any(member => member.Locations.FirstOrDefault() is { SourceTree: { } tree } location && (tree != identifier.SyntaxTree || identifier.SpanStart < location.SourceSpan.Start)))
                    {
                        yield return new DiagnosticModel(SmartEnumDiagnosticCodes.Member, LocationInfo.From(identifier.GetLocation()), type.Name, "All is read before every member is declared; declare the lookup after the members");
                    }
                }
            }
        }
    }
}
