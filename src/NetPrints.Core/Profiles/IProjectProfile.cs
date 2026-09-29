#nullable enable
using System.Collections.Generic;

namespace NetPrints.Core;

/// <summary>
/// Describes a kind of project NetPrints can create and work with (extension-points.md §5): the
/// <c>.csproj</c> template used by <c>IProjectSystem.CreateAsync</c> and legacy conversion, the base
/// types and class templates offered for new classes, and the reverse-DNS id stored in the project
/// file's <c>NetPrintsProfile</c> property. Extensions contribute profiles through
/// <c>IExtensionBuilder</c>; <c>DefaultProjectProfile</c> is the one built-in profile.
/// </summary>
public interface IProjectProfile
{
    /// <summary>
    /// Reverse-DNS identifier of the profile, stored as the project's <c>NetPrintsProfile</c> property
    /// (e.g. <c>"netprints.default"</c>, <c>"netprints.unreal"</c>). Unique across all loaded profiles.
    /// </summary>
    string Id { get; }

    /// <summary>Name shown for this profile in the New Project dialog.</summary>
    string DisplayName { get; }

    /// <summary>Target framework moniker a new project of this profile targets (e.g. <c>"net10.0"</c>).</summary>
    string DefaultTargetFramework { get; }

    /// <summary>
    /// The new project's <c>.csproj</c> text (project-system.md §1), with placeholders
    /// <c>{ProjectName}</c>, <c>{RootNamespace}</c>, <c>{TargetFramework}</c>,
    /// <c>{NetPrintsSdkVersion}</c> and <c>{ProfileId}</c> substituted by
    /// <c>IProjectSystem.CreateAsync</c> and <c>ProjectConverter</c>.
    /// </summary>
    string ProjectTemplate { get; }

    /// <summary>Base types offered when creating a new class; the first entry is the default.</summary>
    IReadOnlyList<TypeSpecifier> BaseTypes { get; }

    /// <summary>Class templates offered by the New Class dialog for this profile.</summary>
    IReadOnlyList<ClassTemplate> ClassTemplates { get; }

    /// <summary>
    /// Identifier of the reflection catalog profile this project profile uses (P2 catalog tooling), or
    /// <see langword="null"/> if it does not customize the catalog. Unused in P1.
    /// </summary>
    string? CatalogProfileId { get; }
}
