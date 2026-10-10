#nullable enable
using NetPrints.Core;

namespace NetPrints.Reflection;

/// <summary>
/// Tells which catalog, if any, hides a type from type-scoped queries (research R10, FR-091).
/// </summary>
public interface ICatalogScope
{
    /// <summary>
    /// Gets the id of the catalog that covers the assembly declaring <paramref name="type"/> but does not list it.
    /// </summary>
    /// <param name="type">The type a scoped query asks about.</param>
    /// <returns>The catalog id, or <see langword="null"/> when no catalog hides the type.</returns>
    string? GetHidingCatalogId(TypeSpecifier type);
}
