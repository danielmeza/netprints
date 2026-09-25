#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;

namespace NetPrints.Core
{
    /// <summary>
    /// A compilation reference to another project's source directory: its .cs files (excluding
    /// bin/obj) are compiled directly alongside the referencing project's, rather than referencing a
    /// built assembly.
    /// </summary>
    [DataContract]
    public class SourceDirectoryReference : CompilationReference
    {
        /// <summary>
        /// All source file paths in the source directory.
        /// </summary>
        public IEnumerable<string> SourceFilePaths
        {
            get
            {
                return Directory.GetFiles(SourceDirectory, "*.cs", SearchOption.AllDirectories).Where(p => !p.Contains("obj" + Path.DirectorySeparatorChar) && !p.Contains("bin" + Path.DirectorySeparatorChar));
            }
        }

        /// <summary>
        /// Whether to include the source files in compilation.
        /// </summary>
        [DataMember]
        public bool IncludeInCompilation
        {
            get;
            set;
        }

        /// <summary>
        /// Path of source directory.
        /// </summary>
        [DataMember]
        public string SourceDirectory
        {
            get;
            private set;
        }

        /// <summary>
        /// Creates a reference to the source directory at <paramref name="directory"/>.
        /// </summary>
        /// <param name="directory">Path of the source directory.</param>
        /// <param name="includeInCompilation">Whether to include the source files in compilation.</param>
        public SourceDirectoryReference(string directory, bool includeInCompilation = false)
        {
            SourceDirectory = directory;
            IncludeInCompilation = includeInCompilation;
        }

        /// <summary>
        /// Returns "Source files at " followed by <see cref="SourceDirectory"/>.
        /// </summary>
        /// <returns>"Source files at " followed by <see cref="SourceDirectory"/>.</returns>
        public override string ToString() => $"Source files at {SourceDirectory}";
    }
}
