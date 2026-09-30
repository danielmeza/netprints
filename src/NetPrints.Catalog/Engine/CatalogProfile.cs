using System.Collections.Generic;

namespace NetPrints.Catalog;

/// <summary>Which symbols a profile starts from, before its globs and rules apply.</summary>
public enum CatalogProfileBase
{
    /// <summary>Every public type and member.</summary>
    PublicApi,

    /// <summary>Only symbols carrying the <c>NetPrints*</c> annotations.</summary>
    Annotated,

    /// <summary>Nothing: only what the rules select.</summary>
    None,
}

/// <summary>What a profile does with obsolete symbols.</summary>
public enum CatalogObsoleteMode
{
    /// <summary>Keep every obsolete symbol.</summary>
    Include,

    /// <summary>Drop every obsolete symbol.</summary>
    Exclude,

    /// <summary>Drop symbols whose <c>[Obsolete]</c> is an error, keep the others.</summary>
    ExcludeErrors,
}

/// <summary>Whether an attribute rule keeps or drops the symbols it matches.</summary>
public enum CatalogAttributeRuleKind
{
    /// <summary>Only symbols with a matching attribute are kept.</summary>
    Require,

    /// <summary>Symbols with a matching attribute are dropped.</summary>
    Exclude,
}

/// <summary>
/// Narrows an attribute rule to one argument: by <see cref="Name"/> (a named argument or constructor parameter)
/// or by <see cref="Position"/> (a constructor argument index), compared with <see cref="EqualsValue"/> (the
/// rendered value) or <see cref="ContainsValue"/> (a member of a flags value or a substring).
/// </summary>
/// <param name="Name">The argument name; exactly one of <paramref name="Name"/> and <paramref name="Position"/> is set.</param>
/// <param name="Position">The zero-based constructor argument index.</param>
/// <param name="EqualsValue">The value the argument must equal; exactly one of this and <paramref name="ContainsValue"/> is set.</param>
/// <param name="ContainsValue">The value the argument must contain.</param>
public sealed record CatalogArgumentMatch(string? Name = null, int? Position = null, string? EqualsValue = null, string? ContainsValue = null);

/// <summary>Selects symbols by attribute (and optionally one attribute argument).</summary>
/// <param name="Rule">Keep or drop the matches.</param>
/// <param name="Attribute">The attribute's full name, for example <c>UnrealSharp.Attributes.UFunctionAttribute</c>.</param>
/// <param name="Argument">An argument the attribute must have, when set.</param>
public sealed record CatalogAttributeRule(CatalogAttributeRuleKind Rule, string Attribute, CatalogArgumentMatch? Argument = null);

/// <summary>
/// Decides which types and members of an assembly end up in a catalog (a <c>*.npprofile.json</c> file or an
/// inline profile). Evaluated in order: base, namespace globs, type globs, type attribute rules, member
/// attribute rules, the obsolete rule, then <c>[NetPrintsIgnore]</c>, which always excludes.
/// </summary>
/// <param name="Id">The profile id: <c>[a-z0-9][a-z0-9._-]*</c>; <c>public-api</c> and <c>annotated</c> are reserved.</param>
/// <param name="Base">The starting selection; defaults to <see cref="CatalogProfileBase.PublicApi"/>.</param>
/// <param name="IncludeNamespaces">Namespace globs to keep; null or empty means all.</param>
/// <param name="ExcludeNamespaces">Namespace globs to drop.</param>
/// <param name="IncludeTypes">Full type name globs to keep; null or empty means all.</param>
/// <param name="ExcludeTypes">Full type name globs to drop.</param>
/// <param name="TypeAttributes">Attribute rules over types.</param>
/// <param name="MemberAttributes">Attribute rules over members.</param>
/// <param name="Obsolete">The obsolete rule; defaults to <see cref="CatalogObsoleteMode.ExcludeErrors"/>.</param>
/// <param name="SchemaVersion">The profile schema version this value was read as.</param>
public sealed record CatalogProfile(
    string Id,
    CatalogProfileBase Base = CatalogProfileBase.PublicApi,
    IReadOnlyList<string>? IncludeNamespaces = null,
    IReadOnlyList<string>? ExcludeNamespaces = null,
    IReadOnlyList<string>? IncludeTypes = null,
    IReadOnlyList<string>? ExcludeTypes = null,
    IReadOnlyList<CatalogAttributeRule>? TypeAttributes = null,
    IReadOnlyList<CatalogAttributeRule>? MemberAttributes = null,
    CatalogObsoleteMode Obsolete = CatalogObsoleteMode.ExcludeErrors,
    int SchemaVersion = CatalogProfile.CurrentSchemaVersion)
{
    /// <summary>The profile schema version this build reads and writes.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>The id of the built-in profile over every public symbol.</summary>
    public const string PublicApiId = "public-api";

    /// <summary>The id of the built-in profile over annotated symbols.</summary>
    public const string AnnotatedId = "annotated";
}
