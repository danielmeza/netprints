using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using NetPrints.Core;
using Xunit;

namespace NetPrints.Tests
{
    public class ReferenceAssemblyResolverTests
    {
        [Fact]
        public void ExistingPathIsKeptAsIs()
        {
            string existing = typeof(object).Assembly.Location;
            var resolver = new ReferenceAssemblyResolver();
            var warnings = new List<string>();

            var paths = resolver.ResolveAssemblyPaths(new[] { new AssemblyReference(existing) }, warnings);

            Assert.Equal(new[] { existing }, paths.ToArray());
            Assert.Empty(warnings);
            Assert.False(resolver.UsesRuntimeAssemblies);
        }

        [Fact]
        public void MissingFrameworkReferenceExpandsOnceToRuntimeAssemblies()
        {
            var resolver = new ReferenceAssemblyResolver();
            var warnings = new List<string>();
            var references = new[]
            {
                new FrameworkAssemblyReference("DoesNotExist/v4.5/System.dll"),
                new FrameworkAssemblyReference("DoesNotExist/v4.5/mscorlib.dll"),
            };

            var paths = resolver.ResolveAssemblyPaths(references, warnings);
            var fileNames = paths.Select(Path.GetFileName).ToList();

            Assert.True(resolver.UsesRuntimeAssemblies);
            Assert.Empty(warnings);
            Assert.True(paths.Count > 50);
            Assert.Contains("System.Private.CoreLib.dll", fileNames);
            Assert.Contains("mscorlib.dll", fileNames);
            // Expanded once, not once per missing framework reference.
            Assert.Equal(paths.Count, paths.Distinct().ToList().Count());
            string runtimeDir = RuntimeEnvironment.GetRuntimeDirectory();
            Assert.True(paths.All(p => p.StartsWith(runtimeDir, System.StringComparison.Ordinal)));
        }

        [Fact]
        public void MissingPlainAssemblyIsSkippedWithWarning()
        {
            var resolver = new ReferenceAssemblyResolver();
            var warnings = new List<string>();
            string missing = Path.Combine(Path.GetTempPath(), "netprints-missing-" + System.Guid.NewGuid() + ".dll");

            var paths = resolver.ResolveAssemblyPaths(new[] { new AssemblyReference(missing) }, warnings);

            Assert.Empty(paths);
            Assert.Single(warnings);
            Assert.Contains(missing, warnings[0]);
            Assert.False(resolver.UsesRuntimeAssemblies);
        }

        [Fact]
        public void RuntimeAssembliesExcludeNativeLibraries()
        {
            var paths = ReferenceAssemblyResolver.GetRuntimeAssemblyPaths();
            Assert.True(paths.All(p => p.EndsWith(".dll", System.StringComparison.OrdinalIgnoreCase)));
            foreach (var path in paths)
            {
                // Throws for native images; the resolver must have filtered those out.
                System.Reflection.AssemblyName.GetAssemblyName(path);
            }
        }

        [Fact]
        public void MissingFrameworkReferenceReplacesAllFrameworkReferences()
        {
            // A project whose v4.5 packs are missing but which also references an installed
            // framework pack: mixing both sets gives Roslyn two corlibs, so the fallback is
            // all-or-nothing for framework references.
            string existing = typeof(object).Assembly.Location;
            var installed = new FrameworkAssemblyReference(".NETFramework/v4.7.2/mscorlib.dll") { AssemblyPath = existing };
            var missing = new FrameworkAssemblyReference("DoesNotExist/v4.5/System.dll");
            var resolver = new ReferenceAssemblyResolver();
            var warnings = new List<string>();

            var paths = resolver.ResolveAssemblyPaths(new AssemblyReference[] { installed, missing }, warnings);

            Assert.True(resolver.UsesRuntimeAssemblies);
            Assert.Equal(ReferenceAssemblyResolver.GetRuntimeAssemblyPaths(), paths);
            string warning = Assert.Single(warnings);
            Assert.Contains(".NETFramework/v4.7.2/mscorlib.dll", warning);
        }

        [Fact]
        public void PlainAssembliesAreKeptNextToTheRuntimeFallback()
        {
            string library = typeof(ReferenceAssemblyResolverTests).Assembly.Location;
            var resolver = new ReferenceAssemblyResolver();
            var warnings = new List<string>();

            var paths = resolver.ResolveAssemblyPaths(new AssemblyReference[]
            {
                new FrameworkAssemblyReference("DoesNotExist/v4.5/System.dll"),
                new AssemblyReference(library),
            }, warnings);

            Assert.Contains(library, paths);
            Assert.Empty(warnings);
        }
    }
}
