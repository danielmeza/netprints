#nullable enable
using System.Runtime.Serialization;

namespace NetPrints.Core
{
    /// <summary>
    /// Abstract base class for a project's compilation references: <see cref="AssemblyReference"/> (a
    /// NuGet/compiled assembly), <see cref="FrameworkAssemblyReference"/> (a framework assembly by
    /// name) and <see cref="SourceDirectoryReference"/> (another project's source directory).
    /// </summary>
    [DataContract]
    [KnownType(typeof(AssemblyReference))]
    [KnownType(typeof(FrameworkAssemblyReference))]
    [KnownType(typeof(SourceDirectoryReference))]
    public abstract class CompilationReference : ICompilationReference
    {
    }
}
