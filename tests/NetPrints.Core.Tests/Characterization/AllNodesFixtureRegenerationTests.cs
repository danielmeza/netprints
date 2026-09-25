using System;
using System.IO;
using System.Linq;
using NetPrints.Tests.Samples;
using Xunit;

namespace NetPrints.Tests.Characterization
{
    /// <summary>
    /// The checked-in <c>AllNodes</c> legacy fixture (<see cref="AllNodesFixtureFactory"/>) is exactly
    /// what the factory produces. Set NETPRINTS_REGENERATE_SAMPLES=1 to rewrite
    /// tests/NetPrints.Core.Tests/Fixtures/Legacy/AllNodes from the factory, and to refresh the
    /// tracked copy of the HelloWorld legacy fixture from samples/HelloWorld.
    /// </summary>
    public class AllNodesFixtureRegenerationTests : IDisposable
    {
        private readonly string tempDir;

        public AllNodesFixtureRegenerationTests()
        {
            tempDir = Path.Combine(Path.GetTempPath(), "netprints-allnodes-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
        }

        public void Dispose()
        {
            try
            { Directory.Delete(tempDir, true); }
            catch (IOException) { }
        }

        [Fact]
        public void FactoryMatchesCheckedInFixture()
        {
            string fixtureDir = Path.Combine(SampleProjectFactory.FindRepositoryRoot(), "tests", "NetPrints.Core.Tests", "Fixtures", "Legacy", "AllNodes");

            if (Environment.GetEnvironmentVariable(AllNodesFixtureFactory.RegenerateVariable) == "1")
            {
                Directory.CreateDirectory(fixtureDir);
                AllNodesFixtureFactory.CreateAllNodes(Path.Combine(fixtureDir, "AllNodes.netpp")).Save();

                string helloWorldSource = Path.Combine(SampleProjectFactory.FindRepositoryRoot(), "samples", "HelloWorld");
                string helloWorldFixtureDir = Path.Combine(SampleProjectFactory.FindRepositoryRoot(), "tests", "NetPrints.Core.Tests", "Fixtures", "Legacy", "HelloWorld");
                Directory.CreateDirectory(helloWorldFixtureDir);
                foreach (string file in Directory.GetFiles(helloWorldSource))
                {
                    File.Copy(file, Path.Combine(helloWorldFixtureDir, Path.GetFileName(file)), overwrite: true);
                }
            }

            AllNodesFixtureFactory.CreateAllNodes(Path.Combine(tempDir, "AllNodes.netpp")).Save();

            var generated = Directory.GetFiles(tempDir).Select(Path.GetFileName).OrderBy(f => f, StringComparer.Ordinal).ToList();
            var checkedIn = Directory.GetFiles(fixtureDir).Select(Path.GetFileName).OrderBy(f => f, StringComparer.Ordinal).ToList();
            Assert.Equal(checkedIn, generated);

            foreach (string file in generated)
            {
                Assert.True(File.ReadAllBytes(Path.Combine(fixtureDir, file)).SequenceEqual(File.ReadAllBytes(Path.Combine(tempDir, file))),
                    $"{file} differs from the factory output; regenerate with {AllNodesFixtureFactory.RegenerateVariable}=1");
            }
        }
    }
}
