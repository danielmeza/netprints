#nullable enable
using System;
using System.IO;
using System.Runtime.Serialization;

namespace NetPrints.Core
{
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

        public FrameworkAssemblyReference(string relativePath)
            : base(ComputeAssemblyPath(relativePath))
        {
            frameworkRelativePath = relativePath;
        }

        private static string ComputeAssemblyPath(string relativePath) =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Reference Assemblies", "Microsoft", "Framework", relativePath);

        public override string ToString() =>
            $"Framework reference assembly {FrameworkRelativePath} found at {AssemblyPath}";
    }
}
