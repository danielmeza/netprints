using CommunityToolkit.Mvvm.ComponentModel;
using NetPrints.Editor.Hosting;
using NetPrints.Projects;

namespace NetPrints.Editor.References;

/// <summary>
/// One entry of the References dialog (PAR-16, PAR-19), wrapping a
/// <see cref="ProjectReferenceInfo"/> declared in the project file (project-system.md §4).
/// </summary>
public sealed class DeclaredReferenceVM(ProjectReferenceInfo info, ReferenceListVM owner) : ObservableObject
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
    /// <c>None</c> item (excluded). Setting it applies
    /// <see cref="ProjectEdit.SetSourceDirectoryIncluded"/> through the owning
    /// <see cref="ReferenceListVM"/>; always <see langword="false"/> for any other reference kind.
    /// </summary>
    /// <exception cref="InvalidOperationException">Set on a reference that is not a source directory.</exception>
    public bool IncludeInCompilation
    {
        get => Info.Kind == DeclaredReferenceKind.SourceDirectory && Info.Included;
        set
        {
            if (Info.Kind != DeclaredReferenceKind.SourceDirectory)
            {
                throw new InvalidOperationException("Only source directory references can be included in compilation.");
            }

            if (Info.Included != value)
            {
                owner.SetSourceDirectoryIncludedAsync(Info.Include, value).Forget(owner.Logger);
            }
        }
    }
}
