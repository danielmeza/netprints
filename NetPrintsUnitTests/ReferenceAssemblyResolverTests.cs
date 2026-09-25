using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetPrints.Core;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace NetPrints.Tests
{
    [TestClass]
    public class ReferenceAssemblyResolverTests
    {
        [TestMethod]
        public void ExistingPathIsKeptAsIs()
        {
            string existing = typeof(object).Assembly.Location;
            var resolver = new ReferenceAssemblyResolver();
            var warnings = new List<string>();

            var paths = resolver.ResolveAssemblyPaths(new[] { new AssemblyReference(existing) }, warnings);

            CollectionAssert.AreEqual(new[] { existing }, paths.ToArray());
            Assert.IsEmpty(warnings);
            Assert.IsFalse(resolver.UsesRuntimeAssemblies);
        }

        [TestMethod]
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

            Assert.IsTrue(resolver.UsesRuntimeAssemblies);
            Assert.IsEmpty(warnings);
            Assert.IsGreaterThan(50, paths.Count);
            CollectionAssert.Contains(fileNames, "System.Private.CoreLib.dll");
            CollectionAssert.Contains(fileNames, "mscorlib.dll");
            // Expanded once, not once per missing framework reference.
            Assert.HasCount(paths.Count, paths.Distinct().ToList());
            string runtimeDir = RuntimeEnvironment.GetRuntimeDirectory();
            Assert.IsTrue(paths.All(p => p.StartsWith(runtimeDir, System.StringComparison.Ordinal)));
        }

        [TestMethod]
        public void MissingPlainAssemblyIsSkippedWithWarning()
        {
            var resolver = new ReferenceAssemblyResolver();
            var warnings = new List<string>();
            string missing = Path.Combine(Path.GetTempPath(), "netprints-missing-" + System.Guid.NewGuid() + ".dll");

            var paths = resolver.ResolveAssemblyPaths(new[] { new AssemblyReference(missing) }, warnings);

            Assert.IsEmpty(paths);
            Assert.HasCount(1, warnings);
            StringAssert.Contains(warnings[0], missing);
            Assert.IsFalse(resolver.UsesRuntimeAssemblies);
        }

        [TestMethod]
        public void RuntimeAssembliesExcludeNativeLibraries()
        {
            var paths = ReferenceAssemblyResolver.GetRuntimeAssemblyPaths();
            Assert.IsTrue(paths.All(p => p.EndsWith(".dll", System.StringComparison.OrdinalIgnoreCase)));
            foreach (var path in paths)
            {
                // Throws for native images; the resolver must have filtered those out.
                System.Reflection.AssemblyName.GetAssemblyName(path);
            }
        }
    }
}
