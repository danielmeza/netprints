using Xunit;
using NetPrints.Core;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace NetPrints.Tests.Samples
{
    public class HelloWorldSampleTests : IDisposable
    {
        private string tempDir;

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
        /// Regenerates samples/HelloWorld when NETPRINTS_REGENERATE_SAMPLES=1; otherwise checks
        /// that the factory still produces a project equivalent to the checked-in one.
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

            Project project = Project.LoadFromPath(Path.Combine(sampleDir, "HelloWorld.netpp"));
            Assert.Equal("HelloWorld", project.Name);
            Assert.Equal(BinaryType.Executable, project.OutputBinaryType);
            ClassGraph cls = project.Classes.Single();
            Assert.Equal("HelloWorld.Program", cls.FullName);
            Assert.Equal("Main", cls.Methods.Single().Name);
        }

        [Fact(Timeout = 120000)]
        public void SampleLoadsCompilesAndPrintsHelloWorld()
        {
            // The sample is linked into the test output (samples/**) by the test project.
            string source = Path.Combine(AppContext.BaseDirectory, "samples", "HelloWorld");
            foreach (string file in Directory.GetFiles(source))
            {
                File.Copy(file, Path.Combine(tempDir, Path.GetFileName(file)));
            }

            Project project = Project.LoadFromPath(Path.Combine(tempDir, "HelloWorld.netpp"));
            Assert.Single(project.Classes);

            project.CompileProject();
            var sw = Stopwatch.StartNew();
            while (project.IsCompiling && sw.Elapsed < TimeSpan.FromSeconds(90))
            {
                Thread.Sleep(50);
            }

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
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            Assert.True(process.WaitForExit(60000));

            Assert.Equal(0, process.ExitCode);
            Assert.Equal("Hello, World!", output.Trim());
        }
    }
}
