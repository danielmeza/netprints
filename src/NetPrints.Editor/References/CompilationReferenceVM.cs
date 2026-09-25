using CommunityToolkit.Mvvm.ComponentModel;
using NetPrints.Core;

namespace NetPrints.Editor.References;

/// <summary>
/// One entry of the References dialog (PAR-16, PAR-19).
/// </summary>
public sealed class CompilationReferenceVM(CompilationReference reference) : ObservableObject
{
    /// <summary>The wrapped model reference.</summary>
    public CompilationReference Reference { get; } = reference;

    /// <summary>The reference's display string (<c>Reference.ToString()</c>).</summary>
    public string DisplayText => Reference.ToString() ?? "";

    /// <summary>Include/Exclude only applies to source directories.</summary>
    public bool ShowIncludeInCompilation => Reference is SourceDirectoryReference;

    /// <summary>
    /// Whether a <see cref="SourceDirectoryReference"/> is included in compilation. Always
    /// <see langword="false"/> for any other reference kind.
    /// </summary>
    /// <exception cref="InvalidOperationException">Set on a reference that is not a <see cref="SourceDirectoryReference"/>.</exception>
    public bool IncludeInCompilation
    {
        get => Reference is SourceDirectoryReference { IncludeInCompilation: true };
        set
        {
            if (Reference is not SourceDirectoryReference sourceReference)
            {
                throw new InvalidOperationException("Only source directory references can be included in compilation.");
            }

            if (sourceReference.IncludeInCompilation != value)
            {
                sourceReference.IncludeInCompilation = value;
                OnPropertyChanged();
            }
        }
    }
}
