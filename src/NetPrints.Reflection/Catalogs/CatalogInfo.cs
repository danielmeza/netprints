#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
namespace NetPrints.Reflection;

/// <summary>
/// Identity and coverage of an <see cref="ITypeCatalog"/> (extension-points.md §4).
/// </summary>
/// <param name="Id">Stable catalog id, unique within a registry.</param>
/// <param name="Version">Version of the catalog's contents.</param>
/// <param name="CoveredAssemblyNames">Simple names (for example <c>"UnrealSharp"</c>) of the assemblies whose
/// types the catalog stands in for. The live provider skips every type declared in one of them.</param>
public sealed record CatalogInfo(string Id, string Version, IReadOnlyList<string> CoveredAssemblyNames);
