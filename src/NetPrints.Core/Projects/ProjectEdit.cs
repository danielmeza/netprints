#nullable enable
using NetPrints.Core;

namespace NetPrints.Projects;

/// <summary>
/// One change <see cref="IProjectSystem.ApplyAsync"/> makes to a project file, preserving every part
/// of the file it does not touch (project-system.md §4: comments, formatting, item order).
/// </summary>
public abstract record ProjectEdit
{
    /// <summary>Sets the project's <c>OutputType</c> (project-system.md §1).</summary>
    /// <param name="Value">The new output type.</param>
    public sealed record SetOutputType(BinaryType Value) : ProjectEdit;

    /// <summary>Sets the project's <c>NetPrintsProfile</c>.</summary>
    /// <param name="ProfileId">Id of the profile to switch to.</param>
    public sealed record SetProfile(string ProfileId) : ProjectEdit;

    /// <summary>
    /// Adds a <c>Reference</c> item with a <c>HintPath</c> (the References dialog's "Add assembly"). A
    /// duplicate of an existing <c>HintPath</c> (case-insensitive, PAR-17) is a no-op.
    /// </summary>
    /// <param name="AssemblyPath">Path of the assembly to reference; stored relative to the project
    /// directory when it is under it, absolute otherwise.</param>
    public sealed record AddAssemblyReference(string AssemblyPath) : ProjectEdit;

    /// <summary>
    /// Adds a <c>Compile</c> item for a source directory (the References dialog's "Add source
    /// directory"), marked <c>NetPrintsSourceDirectory="true"</c>.
    /// </summary>
    /// <param name="DirectoryPath">Path of the directory to include.</param>
    public sealed record AddSourceDirectory(string DirectoryPath) : ProjectEdit;

    /// <summary>
    /// Moves a source directory's item between <c>Compile</c> (included) and <c>None</c> (excluded),
    /// keeping its <c>NetPrintsSourceDirectory="true"</c> marker.
    /// </summary>
    /// <param name="DirectoryPath">Path of the source directory to toggle.</param>
    /// <param name="Included">Whether the directory should be a <c>Compile</c> item afterward.</param>
    public sealed record SetSourceDirectoryIncluded(string DirectoryPath, bool Included) : ProjectEdit;

    /// <summary>
    /// Removes a reference item. Only <see cref="ProjectReferenceInfo.Editable"/> references can be
    /// removed this way; attempting to remove a non-editable one throws
    /// <see cref="System.ArgumentException"/> (project-system.md §4).
    /// </summary>
    /// <param name="Kind">Kind of the reference to remove.</param>
    /// <param name="Include">The reference's <c>Include</c> value.</param>
    public sealed record RemoveReference(DeclaredReferenceKind Kind, string Include) : ProjectEdit;

    /// <summary>
    /// Adds a <c>PackageReference</c> to <c>NetPrints.Sdk</c> (the "Add NetPrints.Sdk" banner shown
    /// when <see cref="ProjectSnapshot.ReferencesNetPrintsSdk"/> is <see langword="false"/>).
    /// </summary>
    /// <param name="Version">Package version to reference.</param>
    public sealed record AddNetPrintsSdk(string Version) : ProjectEdit;
}
