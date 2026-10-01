using System;
using Microsoft.CodeAnalysis;

namespace NetPrints.Catalog;

/// <summary>Documentation comment ids of Roslyn symbols, the ids catalogs use for types and members.</summary>
internal static class SymbolIds
{
    private const int PrefixLength = 2;

    /// <summary>Gets the documentation comment id of a symbol, for example <c>T:Ns.Outer.Inner`1</c> or <c>M:Ns.Type.Method(System.Int32@)</c>.</summary>
    /// <param name="symbol">The symbol.</param>
    /// <returns>The id.</returns>
    /// <exception cref="ArgumentException">The symbol has no documentation comment id.</exception>
    public static string Of(ISymbol symbol)
    {
        Guard.NotNull(symbol, nameof(symbol));
        return symbol.GetDocumentationCommentId() ?? throw new ArgumentException($"'{symbol.Name}' has no documentation comment id.", nameof(symbol));
    }

    /// <summary>Gets the full name of a type as profile globs match it: the type id without its prefix, nested types separated by dots and generic arity as <c>`n</c>.</summary>
    /// <param name="type">The type.</param>
    /// <returns>The name, for example <c>Ns.Outer`1.Inner</c>.</returns>
    public static string FullName(INamedTypeSymbol type) => Of(Guard.NotNull(type, nameof(type))).Substring(PrefixLength);
}
