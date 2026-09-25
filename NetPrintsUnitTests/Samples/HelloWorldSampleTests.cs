using Xunit;
using NetPrints.Core;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace NetPrints.Tests.Samples
{
    public class HelloWorldSampleTests : IDisposable
    {
        private readonly string tempDir;

        public HelloWorldSampleTests()
        {
            tempDir = Path.Combine(Path.GetTempPath(), "netprints-sample-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(tempDir, true); } catch (IOException) { }
        }

        /// <summary>
        /// The checked-in sample is exactly what <see cref="SampleProjectFactory"/> produces.
        /// Set NETPRINTS_REGENERATE_SAMPLES=1 to rewrite samples/HelloWorld from the factory.
        /// </summary>
        [Fact]
        public void FactoryMatchesCheckedInSample()
        {
            string sampleDir = Path.Combine(SampleProjectFactory.FindRepositoryRoot(), "samples", "HelloWorld");

            if (Environment.GetEnvironmentVariable(SampleProjectFactory.RegenerateVariable) == "1")
            {
                Directory.CreateDirectory(sampleDir);
                SampleProjectFactory.CreateHelloWorld(Path.Combine(sampleDir, "HelloWorld.netpp")).Save();
            }

            SampleProjectFactory.CreateHelloWorld(Path.Combine(tempDir, "HelloWorld.netpp")).Save();

            var generated = Directory.GetFiles(tempDir).Select(Path.GetFileName).OrderBy(f => f, StringComparer.Ordinal).ToList();
            var checkedIn = Directory.GetFiles(sampleDir).Select(Path.GetFileName).OrderBy(f => f, StringComparer.Ordinal).ToList();
            Assert.Equal(checkedIn, generated);

            foreach (string file in generated)
            {
                Assert.True(File.ReadAllBytes(Path.Combine(sampleDir, file)).SequenceEqual(File.ReadAllBytes(Path.Combine(tempDir, file))),
                    $"{file} differs from the factory output; regenerate with {SampleProjectFactory.RegenerateVariable}=1");
            }
        }

        [Fact(Timeout = 120000)]
        public async Task SampleLoadsCompilesAndPrintsHelloWorld()
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            // The sample is linked into the test output (samples/**) by the test project.
            string source = Path.Combine(AppContext.BaseDirectory, "samples", "HelloWorld");
            foreach (string file in Directory.GetFiles(source))
            {
                File.Copy(file, Path.Combine(tempDir, Path.GetFileName(file)));
            }

            Project project = Project.LoadFromPath(Path.Combine(tempDir, "HelloWorld.netpp"));
            Assert.Single(project.Classes);

            await CompileAsync(project, cancellationToken);

            Assert.True(project.LastCompilationSucceeded, string.Join(Environment.NewLine, project.LastCompileErrors ?? new ObservableRangeCollection<string>()));
            Assert.Equal("Build succeeded", project.CompilationMessage);

            var (fileName, arguments) = project.GetRunCommand();
            var psi = new ProcessStartInfo(fileName, arguments)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            using Process process = Process.Start(psi);
            string output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            string error = await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            Assert.Equal(0, process.ExitCode);
            Assert.True(error.Length == 0, error);
            Assert.Equal("Hello, World!", output.Trim());
        }

        /// <summary>Compiles and waits until the background compilation finished.</summary>
        internal static async Task CompileAsync(Project project, System.Threading.CancellationToken cancellationToken)
        {
            project.CompileProject();
            while (project.IsCompiling)
            {
                await Task.Delay(50, cancellationToken);
            }
        }
    }
}
