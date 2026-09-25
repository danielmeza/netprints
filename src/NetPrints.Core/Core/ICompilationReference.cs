#nullable enable
namespace NetPrints.Core
{
    /// <summary>
    /// Marker interface for a project's compilation references, implemented by
    /// <see cref="CompilationReference"/>. Declares no members; callers use the concrete
    /// <see cref="CompilationReference"/> type (eg. <see cref="Project.References"/>) rather than this
    /// interface.
    /// </summary>
    public interface ICompilationReference
    {
    }
}
