using CommunityToolkit.Mvvm.ComponentModel;
using NetPrints.Projects;

namespace NetPrints.Editor.References;

/// <summary>
/// One entry of the References dialog (PAR-16, PAR-19), wrapping a
/// <see cref="ProjectReferenceInfo"/> declared in the project file (project-system.md §4).
/// </summary>
public sealed class DeclaredReferenceVM(ProjectReferenceInfo info) : ObservableObject
{
    /// <summary>The wrapped reference, as last read from the project's snapshot.</summary>
    public ProjectReferenceInfo Info { get; } = info;

    /// <summary>The reference's display string: its <see cref="ProjectReferenceInfo.Include"/>, with the package version appended for a package reference.</summary>
    public string DisplayText => Info is { Kind: DeclaredReferenceKind.Package, Version: { Length: > 0 } version }
        ? $"{Info.Include} {version}"
        : Info.Include;

    /// <summary>Include/Exclude only applies to source directories (PAR-19).</summary>
    public bool ShowIncludeInCompilation => Info.Kind == DeclaredReferenceKind.SourceDirectory;

    /// <summary>
    /// Whether a source directory reference is a <c>Compile</c> item (included) rather than a
    /// <c>None</c> item (excluded); always <see langword="false"/> for any other reference kind.
    /// Read-only: the toggle switch is bound <c>OneWay</c> and invokes
    /// <see cref="ReferenceListVM.SetSourceDirectoryIncludedCommand"/> instead (R2-10), which toggles
    /// this value through <see cref="ProjectEdit.SetSourceDirectoryIncluded"/> and applies
    /// <see cref="ReferenceListVM"/>'s own concurrency guard.
    /// </summary>
    public bool IncludeInCompilation => Info.Kind == DeclaredReferenceKind.SourceDirectory && Info.Included;
}
