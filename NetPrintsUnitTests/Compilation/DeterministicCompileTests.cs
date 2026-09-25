using Xunit;
using NetPrints.Core;
using NetPrints.Tests.Samples;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace NetPrints.Tests.Compilation
{
    /// <summary>Constitution VI: compiled output is deterministic.</summary>
    public class DeterministicCompileTests : IDisposable
    {
        private readonly string tempDir = Path.Combine(Path.GetTempPath(), "netprints-det-" + Guid.NewGuid().ToString("N"));

        public DeterministicCompileTests() => Directory.CreateDirectory(tempDir);

        public void Dispose()
        {
            try { Directory.Delete(tempDir, true); } catch (IOException) { }
        }

        [Fact(Timeout = 120000)]
        public async Task CompilingTwiceGivesIdenticalBinaries()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var project = SampleProjectFactory.CreateHelloWorld(Path.Combine(tempDir, "HelloWorld.netpp"));

            // Several classes, so the order in which sources reach the compiler matters.
            for (int i = 0; i < 8; i++)
            {
                project.CreateNewClass();
            }

            project.Save();

            byte[] Output() => File.ReadAllBytes(Path.Combine(tempDir, "Compiled_HelloWorld", "HelloWorld.exe"));

            await HelloWorldSampleTests.CompileAsync(project, cancellationToken);
            Assert.True(project.LastCompilationSucceeded, string.Join(Environment.NewLine, project.LastCompileErrors));
            byte[] first = Output();

            for (int run = 0; run < 3; run++)
            {
                await HelloWorldSampleTests.CompileAsync(project, cancellationToken);
                Assert.True(first.SequenceEqual(Output()), $"compilation {run + 2} produced different bytes");
            }
        }
    }
}
