#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using NetPrints.Core;

namespace NetPrints.Serialization.Legacy;

/// <summary>
/// DataContract-compatible copy of the legacy (pre-P1) <c>NetPrints.Core.Project</c> shape, so old
/// <c>.netpp</c> files still deserialize once Core's own project/reference classes are gone
/// (data-model.md §5). Read only by the project converter (project-system.md §5); never written.
/// </summary>
[DataContract(Name = "Project", Namespace = "http://schemas.datacontract.org/2004/07/NetPrints.Core")]
public sealed class LegacyProject
{
    /// <summary>Name of the project.</summary>
    [DataMember]
    public string Name { get; set; } = "";

    /// <summary>Default namespace of newly created classes.</summary>
    [DataMember]
    public string DefaultNamespace { get; set; } = "";

    /// <summary>Relative paths to the project's class files.</summary>
    [DataMember]
    public List<string> ClassPaths { get; set; } = [];

    /// <summary>The project's compilation references.</summary>
    [DataMember]
    public List<LegacyCompilationReference> References { get; set; } = [];

    /// <summary>What compiling the project used to write to its output directory.</summary>
    [DataMember]
    public ProjectCompilationOutput CompilationOutput { get; set; }

    /// <summary>Kind of binary the project used to compile to.</summary>
    [DataMember]
    public BinaryType OutputBinaryType { get; set; }

    /// <summary>Path to the last successfully compiled assembly, or <see langword="null"/> if none.</summary>
    [DataMember]
    public string? LastCompiledAssemblyPath { get; set; }

    /// <summary>Version of the editor the project was last saved with.</summary>
    [DataMember]
    public Version SaveVersion { get; set; } = new(0, 0);
}

/// <summary>
/// DataContract-compatible copy of the legacy <c>NetPrints.Core.CompilationReference</c> hierarchy
/// (data-model.md §5): <see cref="LegacyAssemblyReference"/>, <see cref="LegacyFrameworkAssemblyReference"/>
/// and <see cref="LegacySourceDirectoryReference"/>.
/// </summary>
[DataContract(Name = "CompilationReference", Namespace = "http://schemas.datacontract.org/2004/07/NetPrints.Core")]
[KnownType(typeof(LegacyAssemblyReference))]
[KnownType(typeof(LegacyFrameworkAssemblyReference))]
[KnownType(typeof(LegacySourceDirectoryReference))]
public abstract class LegacyCompilationReference
{
}

/// <summary>Legacy copy of a compilation reference to a single assembly, by file path.</summary>
[DataContract(Name = "AssemblyReference", Namespace = "http://schemas.datacontract.org/2004/07/NetPrints.Core")]
public class LegacyAssemblyReference : LegacyCompilationReference
{
    /// <summary>Path to the referenced assembly file.</summary>
    [DataMember]
    public string AssemblyPath { get; set; } = "";
}

/// <summary>
/// Legacy copy of a compilation reference to a .NET Framework reference assembly, resolved relative to
/// the local "Reference Assemblies\Microsoft\Framework" folder.
/// </summary>
[DataContract(Name = "FrameworkAssemblyReference", Namespace = "http://schemas.datacontract.org/2004/07/NetPrints.Core")]
public sealed class LegacyFrameworkAssemblyReference : LegacyAssemblyReference
{
    /// <summary>
    /// Path relative to the reference assemblies folder. Serialized as <c>frameworkRelativePath</c>: the
    /// original type's private backing field name, not its public property name.
    /// </summary>
    [DataMember(Name = "frameworkRelativePath")]
    public string FrameworkRelativePath { get; set; } = "";
}

/// <summary>Legacy copy of a compilation reference to another project's source directory.</summary>
[DataContract(Name = "SourceDirectoryReference", Namespace = "http://schemas.datacontract.org/2004/07/NetPrints.Core")]
public sealed class LegacySourceDirectoryReference : LegacyCompilationReference
{
    /// <summary>Whether to include the source files in compilation.</summary>
    [DataMember]
    public bool IncludeInCompilation { get; set; }

    /// <summary>Path of the source directory.</summary>
    [DataMember]
    public string SourceDirectory { get; set; } = "";
}
