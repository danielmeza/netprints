using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace NetPrints.Core
{
    /// <summary>
    /// Resolves the assembly references of a project to files that exist on this machine.
    /// </summary>
    /// <remarks>
    /// Minimal, format-preserving cross-platform fallback (P0, FR-008/FR-009):
    /// <list type="bullet">
    /// <item>A reference whose file exists is used as-is (unchanged behavior on Windows with
    /// .NET Framework targeting packs).</item>
    /// <item>A <see cref="FrameworkAssemblyReference"/> whose file is missing (for example the
    /// .NET Framework reference assemblies on Linux) expands, once, to the managed assemblies of
    /// the running .NET runtime.</item>
    /// <item>Any other missing file is skipped and reported as a warning instead of throwing.</item>
    /// </list>
    /// Real reference-pack resolution and target selection are P1 work.
    /// </remarks>
    public sealed class ReferenceAssemblyResolver
    {
        private static readonly Lazy<IReadOnlyList<string>> runtimeAssemblyPaths =
            new Lazy<IReadOnlyList<string>>(FindRuntimeAssemblyPaths);

        /// <summary>
        /// Whether the last call to <see cref="ResolveAssemblyPaths"/> fell back to the running
        /// runtime's assemblies. Compiled executables then need a runtime configuration file
        /// and are started through the <c>dotnet</c> host.
        /// </summary>
        public bool UsesRuntimeAssemblies { get; private set; }

        /// <summary>
        /// Resolves assembly references to existing file paths.
        /// </summary>
        /// <param name="references">Assembly references to resolve.</param>
        /// <param name="warnings">Receives a message for every reference that was skipped.</param>
        /// <returns>Distinct paths of existing assembly files.</returns>
        public IReadOnlyList<string> ResolveAssemblyPaths(IEnumerable<AssemblyReference> references, ICollection<string> warnings)
        {
            if (references is null) throw new ArgumentNullException(nameof(references));
            if (warnings is null) throw new ArgumentNullException(nameof(warnings));

            UsesRuntimeAssemblies = false;

            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void Add(string path)
            {
                if (seen.Add(path))
                {
                    result.Add(path);
                }
            }

            foreach (var reference in references)
            {
                string path = reference.AssemblyPath;

                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    Add(path);
                }
                else if (reference is FrameworkAssemblyReference)
                {
                    if (!UsesRuntimeAssemblies)
                    {
                        UsesRuntimeAssemblies = true;

                        foreach (var runtimePath in GetRuntimeAssemblyPaths())
                        {
                            Add(runtimePath);
                        }
                    }
                }
                else
                {
                    warnings.Add($"Warning: referenced assembly not found and skipped: {path}");
                }
            }

            return result;
        }

        /// <summary>
        /// Paths of the managed assemblies of the running .NET runtime.
        /// </summary>
        public static IReadOnlyList<string> GetRuntimeAssemblyPaths() => runtimeAssemblyPaths.Value;

        /// <summary>
        /// Path of the <c>dotnet</c> host that runs the current process, or <c>"dotnet"</c>
        /// (resolved through <c>PATH</c>) if it cannot be found.
        /// </summary>
        public static string GetDotNetHostPath()
        {
            string hostPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
            if (!string.IsNullOrEmpty(hostPath) && File.Exists(hostPath))
            {
                return hostPath;
            }

            // <root>/shared/Microsoft.NETCore.App/<version>/ -> <root>/dotnet
            string hostName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "dotnet.exe" : "dotnet";
            var runtimeDir = new DirectoryInfo(RuntimeEnvironment.GetRuntimeDirectory());
            string root = runtimeDir.Parent?.Parent?.Parent?.FullName;
            if (root != null)
            {
                string candidate = Path.Combine(root, hostName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return "dotnet";
        }

        private static IReadOnlyList<string> FindRuntimeAssemblyPaths()
        {
            string runtimeDir = RuntimeEnvironment.GetRuntimeDirectory();

            return Directory.EnumerateFiles(runtimeDir, "*.dll")
                .Where(IsManagedAssembly)
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToList();
        }

        private static bool IsManagedAssembly(string path)
        {
            try
            {
                AssemblyName.GetAssemblyName(path);
                return true;
            }
            catch (Exception)
            {
                // Native images (for example coreclr.dll on Windows) throw BadImageFormatException.
                return false;
            }
        }
    }
}
