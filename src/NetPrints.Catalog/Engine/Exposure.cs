using System;
using Microsoft.CodeAnalysis;

namespace NetPrints.Catalog;

/// <summary>What of an assembly a catalog can describe at all, before any profile decides: public types and public or protected members.</summary>
internal static class Exposure
{
    /// <summary>Returns whether <paramref name="type"/> and every type it is nested in are public.</summary>
    public static bool IsExposedType(INamedTypeSymbol type)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.ContainingType)
        {
            if (current.DeclaredAccessibility != Accessibility.Public)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Maps a declared accessibility to a catalog visibility; <see langword="null"/> when it is not visible outside the assembly.</summary>
    public static CatalogVisibility? VisibilityOf(Accessibility accessibility) => accessibility switch
    {
        Accessibility.Public => CatalogVisibility.Public,
        Accessibility.Protected or Accessibility.ProtectedOrInternal => CatalogVisibility.Protected,
        _ => null,
    };

    /// <summary>Returns whether <paramref name="member"/> is a constructor, method, operator, property or field a catalog can list.</summary>
    public static bool IsCatalogMember(ISymbol member)
    {
        if (member.ContainingType is not { } owner || IsHiddenKind(member) || member.Name.IndexOf("<", StringComparison.Ordinal) >= 0)
        {
            return false;
        }

        return VisibilityOf(member.DeclaredAccessibility) switch
        {
            CatalogVisibility.Public => true,
            CatalogVisibility.Protected => !owner.IsSealed,
            _ => false,
        };
    }

    private static bool IsHiddenKind(ISymbol member) => member switch
    {
        IMethodSymbol method => method.MethodKind is not (MethodKind.Constructor or MethodKind.Ordinary or MethodKind.UserDefinedOperator or MethodKind.Conversion)
            || method.ExplicitInterfaceImplementations.Length > 0,
        IPropertySymbol property => property.IsIndexer,
        IFieldSymbol field => field.IsImplicitlyDeclared || field.ContainingType.TypeKind == TypeKind.Enum,
        _ => true,
    };
}
