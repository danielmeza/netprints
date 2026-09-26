using System.IO;
using System.Runtime.Serialization;
using NetPrints.Serialization.Legacy;
using NetPrints.Tests.Samples;
using Xunit;

namespace NetPrints.Tests.Serialization
{
    /// <summary>
    /// <see cref="LegacyProject"/> and its reference hierarchy: both fixtures' <c>.netpp</c> files still
    /// deserialize once Core's own project/reference classes are gone (data-model.md §5).
    /// </summary>
    public class LegacyProjectTests
    {
        private static readonly DataContractSerializer Serializer = new(typeof(LegacyProject));

        private static LegacyProject ReadProject(params string[] relativeSegments)
        {
            string path = Path.Combine(SampleProjectFactory.FindRepositoryRoot(), Path.Combine(relativeSegments));
            using FileStream stream = File.OpenRead(path);
            return Assert.IsType<LegacyProject>(Serializer.ReadObject(stream));
        }

        [Fact]
        public void HelloWorldNetppDeserializes()
        {
            LegacyProject project = ReadProject(
                "tests", "NetPrints.Core.Tests", "Fixtures", "Legacy", "HelloWorld", "HelloWorld.netpp");

            Assert.Equal("HelloWorld", project.Name);
            Assert.NotEmpty(project.ClassPaths);
        }

        [Fact]
        public void AllNodesNetppDeserializesEveryReferenceKind()
        {
            LegacyProject project = ReadProject(
                "tests", "NetPrints.Core.Tests", "Fixtures", "Legacy", "AllNodes", "AllNodes.netpp");

            Assert.Equal("AllNodes", project.Name);
            Assert.Equal("AllNodes", project.DefaultNamespace);
            Assert.Contains("AllNodes.Everything.netpc", project.ClassPaths);

            var framework = Assert.IsType<LegacyFrameworkAssemblyReference>(
                project.References.Find(r => r is LegacyFrameworkAssemblyReference));
            Assert.Equal(".NETFramework/v4.5/System.dll", framework.FrameworkRelativePath);
            Assert.Equal("Reference Assemblies/Microsoft/Framework/.NETFramework/v4.5/System.dll", framework.AssemblyPath);

            var assembly = Assert.IsType<LegacyAssemblyReference>(
                project.References.Find(r => r.GetType() == typeof(LegacyAssemblyReference)));
            Assert.Equal("ExternalLibrary.dll", assembly.AssemblyPath);

            var sourceDirectory = Assert.IsType<LegacySourceDirectoryReference>(
                project.References.Find(r => r is LegacySourceDirectoryReference));
            Assert.Equal("ExternalSources", sourceDirectory.SourceDirectory);
            Assert.False(sourceDirectory.IncludeInCompilation);
        }
    }
}
