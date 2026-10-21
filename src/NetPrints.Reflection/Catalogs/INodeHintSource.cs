#nullable enable
using System.Collections.Generic;
using NetPrints.Core;

namespace NetPrints.Reflection;

/// <summary>
/// The presentation hints a catalog gives a node (<c>[NetPrintsNode]</c>, <c>[NetPrintsType]</c>): how node search titles and keys it.
/// </summary>
/// <param name="DisplayName">The title to show instead of the formatted member, or <see langword="null"/> to keep it.</param>
/// <param name="Category">The palette category the catalog suggests, or <see langword="null"/>.</param>
/// <param name="Keywords">Extra words the search matches; empty when the hint has none.</param>
public sealed record NodeHint(string? DisplayName, string? Category, IReadOnlyList<string> Keywords);

/// <summary>
/// A reflection provider that knows the node hints of the members it lists (AB-10): catalog-backed providers do, and
/// the composite and memoized providers forward to the providers they wrap.
/// </summary>
public interface INodeHintSource
{
    /// <summary>
    /// Gets the hint of a method.
    /// </summary>
    /// <param name="method">The method a node search entry calls.</param>
    /// <returns>The hint, or <see langword="null"/> when no catalog describes the method.</returns>
    NodeHint? GetMethodHint(MethodSpecifier method);

    /// <summary>
    /// Gets the hint of a type.
    /// </summary>
    /// <param name="type">The type a node search entry offers.</param>
    /// <returns>The hint, or <see langword="null"/> when no catalog describes the type.</returns>
    NodeHint? GetTypeHint(TypeSpecifier type);
}
