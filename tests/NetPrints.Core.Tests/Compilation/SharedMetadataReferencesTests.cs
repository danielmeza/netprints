using System;
using System.IO;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using NetPrints.Compilation;
using NetPrints.Projects;
using Xunit;

namespace NetPrints.Tests.Compilation
{
    /// <summary><see cref="SharedMetadataReferences"/>: one reference per assembly file, so compilations do not each read it again.</summary>
    public class SharedMetadataReferencesTests
    {
        private static readonly string AssemblyPath = typeof(object).Assembly.Location;

        [Fact]
        public void TheSameAssemblyYieldsTheSameReferenceWhileItIsAlive()
        {
            MetadataReference first = SharedMetadataReferences.For(new ResolvedAssembly(AssemblyPath, null));
            MetadataReference second = SharedMetadataReferences.For(new ResolvedAssembly(AssemblyPath, null));

            Assert.Same(first, second);
            GC.KeepAlive(first);
        }

        [Fact]
        public void AnotherAssemblyYieldsAnotherReference()
        {
            MetadataReference runtime = SharedMetadataReferences.For(new ResolvedAssembly(AssemblyPath, null));
            MetadataReference other = SharedMetadataReferences.For(new ResolvedAssembly(typeof(Uri).Assembly.Location, null));

            Assert.NotSame(runtime, other);
            GC.KeepAlive(runtime);
        }

        [Fact]
        public void AReferenceNothingUsesAnyMoreIsCollected()
        {
            WeakReference weak = Create();

            for (int pass = 0; pass < 3 && weak.IsAlive; pass++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            Assert.False(weak.IsAlive);
        }

        [Fact]
        public void AChangedFileYieldsANewReference()
        {
            string directory = Path.Combine(Path.GetTempPath(), "netprints-shared-refs", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                string path = Path.Combine(directory, "Copy.dll");
                File.Copy(AssemblyPath, path);
                MetadataReference before = SharedMetadataReferences.For(new ResolvedAssembly(path, null));

                File.SetLastWriteTimeUtc(path, File.GetLastWriteTimeUtc(path).AddMinutes(1));
                MetadataReference after = SharedMetadataReferences.For(new ResolvedAssembly(path, null));

                Assert.NotSame(before, after);
                GC.KeepAlive(before);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [Fact]
        public void AMissingFileThrows() =>
            Assert.Throws<FileNotFoundException>(() => SharedMetadataReferences.For(new ResolvedAssembly(Path.Combine(Path.GetTempPath(), "netprints-missing.dll"), null)));

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference Create() =>
            new(SharedMetadataReferences.For(new ResolvedAssembly(typeof(System.Xml.XmlDocument).Assembly.Location, null)));
    }
}
