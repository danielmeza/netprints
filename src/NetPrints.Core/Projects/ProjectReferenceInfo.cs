#nullable enable

namespace NetPrints.Projects;

/// <summary>
/// Kind of a project reference shown by the References dialog (project-system.md §1, PAR-16…20).
/// </summary>
public enum DeclaredReferenceKind
{
    /// <summary>A NuGet <c>PackageReference</c>.</summary>
    Package,

    /// <summary>A <c>ProjectReference</c> to another project.</summary>
    Project,

    /// <summary>An assembly <c>Reference</c> with a <c>HintPath</c>.</summary>
    Assembly,

    /// <summary>A source directory included or excluded via <c>NetPrintsSourceDirectory="true"</c>.</summary>
    SourceDirectory,
}

/// <summary>
/// One reference declared in the project file, as shown by the References dialog
/// (project-system.md §1, §4).
/// </summary>
/// <param name="Kind">What kind of reference this is.</param>
/// <param name="Include">The item's <c>Include</c> value: a package id, project path, assembly simple
/// name, or source directory path.</param>
/// <param name="Version">The package version, for <see cref="DeclaredReferenceKind.Package"/>;
/// <see langword="null"/> otherwise.</param>
/// <param name="Included">For <see cref="DeclaredReferenceKind.SourceDirectory"/>, whether the
/// directory is currently a <c>Compile</c> item (included) or a <c>None</c> item (excluded); ignored
/// for the other kinds.</param>
/// <param name="Editable">Whether <see cref="ProjectEdit.RemoveReference"/> and (for a source
/// directory) <see cref="ProjectEdit.SetSourceDirectoryIncluded"/> can act on this reference; package
/// and project references are shown read-only in P1.</param>
public sealed record ProjectReferenceInfo(
    DeclaredReferenceKind Kind,
    string Include,
    string? Version,
    bool Included,
    bool Editable);
