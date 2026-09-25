using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetPrints.Core;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace NetPrints.Tests.Samples
{
    [TestClass]
    public class HelloWorldSampleTests
    {
        private string tempDir;

        [TestInitialize]
        public void Setup()
        {
            tempDir = Path.Combine(Path.GetTempPath(), "netprints-sample-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
        }

        [TestCleanup]
        public void Cleanup()
        {
            try { Directory.Delete(tempDir, true); } catch (IOException) { }
        }

        /// <summary>
        /// Regenerates samples/HelloWorld when NETPRINTS_REGENERATE_SAMPLES=1; otherwise checks
        /// that the factory still produces a project equivalent to the checked-in one.
        /// </summary>
        [TestMethod]
        public void FactoryMatchesCheckedInSample()
        {
            string sampleDir = Path.Combine(SampleProjectFactory.FindRepositoryRoot(), "samples", "HelloWorld");

            if (Environment.GetEnvironmentVariable(SampleProjectFactory.RegenerateVariable) == "1")
            {
                Directory.CreateDirectory(sampleDir);
                SampleProjectFactory.CreateHelloWorld(Path.Combine(sampleDir, "HelloWorld.netpp")).Save();
            }

            Project project = Project.LoadFromPath(Path.Combine(sampleDir, "HelloWorld.netpp"));
            Assert.AreEqual("HelloWorld", project.Name);
            Assert.AreEqual(BinaryType.Executable, project.OutputBinaryType);
            ClassGraph cls = project.Classes.Single();
            Assert.AreEqual("HelloWorld.Program", cls.FullName);
            Assert.AreEqual("Main", cls.Methods.Single().Name);
        }

        [TestMethod]
        [Timeout(120000, CooperativeCancellation = true)]
        public void SampleLoadsCompilesAndPrintsHelloWorld()
        {
            // The sample is linked into the test output (samples/**) by the test project.
            string source = Path.Combine(AppContext.BaseDirectory, "samples", "HelloWorld");
            foreach (string file in Directory.GetFiles(source))
            {
                File.Copy(file, Path.Combine(tempDir, Path.GetFileName(file)));
            }

            Project project = Project.LoadFromPath(Path.Combine(tempDir, "HelloWorld.netpp"));
            Assert.AreEqual(1, project.Classes.Count);

            project.CompileProject();
            var sw = Stopwatch.StartNew();
            while (project.IsCompiling && sw.Elapsed < TimeSpan.FromSeconds(90))
            {
                Thread.Sleep(50);
            }

            Assert.IsTrue(project.LastCompilationSucceeded, string.Join(Environment.NewLine, project.LastCompileErrors ?? new ObservableRangeCollection<string>()));
            Assert.AreEqual("Build succeeded", project.CompilationMessage);

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
            Assert.IsTrue(process.WaitForExit(60000));

            Assert.AreEqual(0, process.ExitCode, error);
            Assert.AreEqual("Hello, World!", output.Trim());
        }
    }
}
