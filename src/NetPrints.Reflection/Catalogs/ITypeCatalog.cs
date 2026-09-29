#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
namespace NetPrints.Reflection;

/// <summary>
/// A precomputed, immutable answer to reflection queries for a set of assemblies (extension-points.md §4),
/// contributed by an extension so the editor need not load those assemblies to offer their types.
/// </summary>
public interface ITypeCatalog : IReflectionProvider
{
    /// <summary>
    /// Identity and covered assemblies of this catalog.
    /// </summary>
    CatalogInfo Info { get; }
}
