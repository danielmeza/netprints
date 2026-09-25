#nullable enable
using System;
using System.IO;
using System.Runtime.Serialization;

namespace NetPrints.Core
{
    /// <summary>
    /// A compilation reference to a .NET Framework reference assembly, resolved from its path relative
    /// to the local "Reference Assemblies\Microsoft\Framework" folder (<see cref="Environment.SpecialFolder.ProgramFilesX86"/>).
    /// That folder only exists on Windows with the .NET Framework reference assemblies installed; on
    /// other platforms <see cref="AssemblyReference.AssemblyPath"/> resolves to a path that does not exist.
    /// </summary>
    [DataContract]
    public class FrameworkAssemblyReference : AssemblyReference
    {
        /// <summary>
        /// Path relative to reference assemblies path.
        /// </summary>
        public string FrameworkRelativePath
        {
            get => frameworkRelativePath;
            private set
            {
                frameworkRelativePath = value;
                AssemblyPath = ComputeAssemblyPath(value);
            }
        }

        [DataMember]
        private string frameworkRelativePath;

        /// <summary>
        /// Creates a reference to the framework assembly at <paramref name="relativePath"/>, relative
        /// to the local reference assemblies folder.
        /// </summary>
        /// <param name="relativePath">Path relative to the reference assemblies path.</param>
        public FrameworkAssemblyReference(string relativePath)
            : base(ComputeAssemblyPath(relativePath))
        {
            frameworkRelativePath = relativePath;
        }

        private static string ComputeAssemblyPath(string relativePath) =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Reference Assemblies", "Microsoft", "Framework", relativePath);

        /// <summary>
        /// Returns "Framework reference assembly " followed by <see cref="FrameworkRelativePath"/> and
        /// " found at " and <see cref="AssemblyReference.AssemblyPath"/>.
        /// </summary>
        /// <returns>The reference's display string.</returns>
        public override string ToString() =>
            $"Framework reference assembly {FrameworkRelativePath} found at {AssemblyPath}";
    }
}
