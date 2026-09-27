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
    /// tests/NetPrints.Core.Tests/Fixtures/Legacy/AllNodes from the factory. The legacy
    /// <c>Fixtures/Legacy/HelloWorld</c> copy is frozen (T059/T062a switched
    /// <c>samples/HelloWorld</c> to the <c>.csproj</c> layout, so it no longer holds a
    /// <c>.netpp</c>/<c>.netpc</c> to refresh from) and is not touched by this regeneration path.
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

                // Fixtures/Legacy/HelloWorld is frozen: samples/HelloWorld is .csproj-based now (T059),
                // so it has no .netpp/.netpc left to refresh this legacy copy from.
            }

            AllNodesFixtureFactory.CreateAllNodes(Path.Combine(tempDir, "AllNodes.netpp")).Save();

            var generated = Directory.GetFiles(tempDir).Select(Path.GetFileName).OfType<string>().OrderBy(f => f, StringComparer.Ordinal).ToList();
            var checkedIn = Directory.GetFiles(fixtureDir).Select(Path.GetFileName).OfType<string>().OrderBy(f => f, StringComparer.Ordinal).ToList();
            Assert.Equal(checkedIn, generated);

            foreach (string file in generated)
            {
                Assert.True(File.ReadAllBytes(Path.Combine(fixtureDir, file)).SequenceEqual(File.ReadAllBytes(Path.Combine(tempDir, file))),
                    $"{file} differs from the factory output; regenerate with {AllNodesFixtureFactory.RegenerateVariable}=1");
            }
        }
    }
}
