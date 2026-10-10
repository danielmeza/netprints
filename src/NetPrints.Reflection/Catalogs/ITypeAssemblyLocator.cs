#nullable enable
using NetPrints.Core;

namespace NetPrints.Reflection;

/// <summary>
/// Tells which assembly declares a type, so a composite can route a type-scoped query to the catalog that covers it.
/// </summary>
public interface ITypeAssemblyLocator
{
    /// <summary>
    /// Gets the simple name of the assembly that declares <paramref name="type"/>.
    /// </summary>
    /// <param name="type">The type to locate.</param>
    /// <returns>The assembly's simple name, or <see langword="null"/> when the type is unknown.</returns>
    string? GetAssemblyName(TypeSpecifier type);
}
