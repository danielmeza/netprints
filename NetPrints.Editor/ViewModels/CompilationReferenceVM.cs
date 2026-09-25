using CommunityToolkit.Mvvm.ComponentModel;
using NetPrints.Core;

namespace NetPrints.Editor.ViewModels;

/// <summary>
/// One entry of the References dialog (PAR-16, PAR-19).
/// </summary>
public sealed class CompilationReferenceVM(CompilationReference reference) : ObservableObject
{
    public CompilationReference Reference { get; } = reference;

    public string DisplayText => Reference.ToString() ?? "";

    /// <summary>Include/Exclude only applies to source directories.</summary>
    public bool ShowIncludeInCompilation => Reference is SourceDirectoryReference;

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
