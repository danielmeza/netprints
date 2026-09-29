using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NetPrints.Core;
using NetPrints.Projects;
using NetPrints.Tests.Samples;
using Xunit;

namespace NetPrints.Tests.Compilation
{
    /// <summary>Constitution VI: compiled output is deterministic.</summary>
    public class DeterministicCompileTests : IDisposable
    {
        private readonly string tempDir = Path.Combine(Path.GetTempPath(), "netprints-det-" + Guid.NewGuid().ToString("N"));

        public DeterministicCompileTests() => Directory.CreateDirectory(tempDir);

        public void Dispose()
        {
            try
            { Directory.Delete(tempDir, true); }
            catch (IOException) { }
        }

        private async Task<Dictionary<string, byte[]>> BuildAndRecordAsync(SampleBuild sample, Project project, System.Threading.CancellationToken cancellationToken)
        {
            BuildResult build = await sample.SaveAndBuildAsync(project, cancellationToken);
            Assert.True(build.Success, build.Log);
            string assembly = Assert.IsType<string>(build.OutputAssemblyPath);

            var recorded = new Dictionary<string, byte[]> { [Path.GetFileName(assembly)] = await File.ReadAllBytesAsync(assembly, cancellationToken) };
            foreach (string generated in Directory.EnumerateFiles(tempDir, "*.netpc.g.cs").OrderBy(p => p, StringComparer.Ordinal))
            {
                recorded[Path.GetFileName(generated)] = await File.ReadAllBytesAsync(generated, cancellationToken);
            }

            return recorded;
        }

        [Fact(Timeout = 240000)]
        public async Task BuildingTwiceGivesIdenticalOutput()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            SampleBuild sample = SampleBuild.CopyHelloWorld(tempDir);
            Project project = await sample.LoadAsync(cancellationToken);

            // Several classes, so the order in which sources reach the compiler matters.
            IProjectProfile profile = DefaultProjectProfile.Instance;
            for (int i = 0; i < 8; i++)
            {
                project.CreateNewClass(profile);
            }

            Dictionary<string, byte[]> first = await BuildAndRecordAsync(sample, project, cancellationToken);
            Assert.Equal(9, first.Keys.Count(name => name.EndsWith(".netpc.g.cs", StringComparison.Ordinal)));

            foreach (string folder in new[] { "bin", "obj" })
            {
                string path = Path.Combine(tempDir, folder);
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }

            Dictionary<string, byte[]> second = await BuildAndRecordAsync(sample, project, cancellationToken);

            Assert.Equal(first.Keys.OrderBy(k => k, StringComparer.Ordinal), second.Keys.OrderBy(k => k, StringComparer.Ordinal));
            foreach ((string name, byte[] bytes) in first)
            {
                Assert.True(bytes.AsSpan().SequenceEqual(second[name]), $"{name} differs between the two builds");
            }
        }
    }
}
