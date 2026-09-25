#nullable enable
using System.IO;
using System.Runtime.Serialization;

namespace NetPrints.Core
{
    /// <summary>
    /// A compilation reference to a single assembly, by file path.
    /// </summary>
    [DataContract]
    public class AssemblyReference : CompilationReference
    {
        /// <summary>
        /// Path to the referenced assembly file.
        /// </summary>
        [DataMember]
        public string AssemblyPath
        {
            get;
            set;
        }

        /// <summary>
        /// Creates a reference to the assembly at <paramref name="assemblyPath"/>.
        /// </summary>
        /// <param name="assemblyPath">Path to the referenced assembly file.</param>
        public AssemblyReference(string assemblyPath)
        {
            AssemblyPath = assemblyPath;
        }

        /// <summary>
        /// Returns the assembly's file name without extension, followed by " at " and its full path.
        /// </summary>
        /// <returns>The assembly's file name without extension, followed by " at " and its full path.</returns>
        public override string ToString() => $"{Path.GetFileNameWithoutExtension(AssemblyPath)} at {AssemblyPath}";
    }
}
