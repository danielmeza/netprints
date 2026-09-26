using System;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Core;
using NetPrints.Projects;
using NetPrints.Serialization;
using NetPrints.Serialization.Json;
using NetPrints.Serialization.Mapping;
using NetPrints.Serialization.Migrations;
using NetPrints.Serialization.Stores;
using NetPrints.Tests.Projects;
using NetPrints.Workspace;
using Xunit;

namespace NetPrints.Tests.Samples
{
    /// <summary>
    /// The checked-in <c>samples/HelloWorld</c> project (FR-010): loads through
    /// <see cref="ProjectPersistence"/> and builds/runs through <see cref="IProjectSystem"/>
    /// (project-system.md §4). The canonical-graph and up-to-date-<c>.g.cs</c> checks that used to live
    /// here (DF-T26) moved to <c>CommittedSampleTests</c>; the temp-copy build of AllNodes moved to
    /// <c>MigratedFixtureBuildTests</c>.
    /// </summary>
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
            try
            { Directory.Delete(tempDir, true); }
            catch (IOException) { }
        }

        private static ProjectPersistence NewPersistence(IProjectSystem projects)
        {
            var registry = new NodeDocumentConverterRegistry(NodeDocumentConverterRegistry.BuiltIn, []);
            var mapper = new DocumentMapper(registry);
            var formats = new DocumentFormatRegistry([new JsonDocumentFormat(new NetPrintsJsonOptions(registry), new DocumentMigrator([]))]);
            return new ProjectPersistence(projects, formats, mapper,
                dir => new FileSystemDocumentStore(dir, Scheduler.Default, NullLogger<FileSystemDocumentStore>.Instance),
                NullLogger<ProjectPersistence>.Instance);
        }

        [Fact(Timeout = 120000)]
        public async Task SampleLoadsCompilesAndPrintsHelloWorld()
        {
            CancellationToken cancellationToken = TestContext.Current.CancellationToken;

            // The sample is linked into the test output (samples/**) by the test project.
            string source = Path.Combine(AppContext.BaseDirectory, "samples", "HelloWorld");
            foreach (string file in Directory.GetFiles(source))
            {
                File.Copy(file, Path.Combine(tempDir, Path.GetFileName(file)));
            }

            LocalSdkLayout.Write(tempDir);

            var projectSystem = new MsBuildProjectSystem(new ProjectSystemOptions([], "1.0.0-test"), new ProcessRunner(), NullLogger<MsBuildProjectSystem>.Instance);
            ProjectPersistence persistence = NewPersistence(projectSystem);

            string csprojPath = Path.Combine(tempDir, "HelloWorld.csproj");
            ProjectLoadResult loaded = await persistence.LoadAsync(csprojPath, cancellationToken);
            Assert.Empty(loaded.Issues);
            Assert.Single(loaded.Project.Classes);

            BuildResult build = await projectSystem.BuildAsync(csprojPath, cancellationToken);
            Assert.True(build.Success, build.Log);

            ProcessStartRequest run = projectSystem.GetRunCommand(csprojPath);
            ProcessResult result = await new ProcessRunner().RunAsync(run, cancellationToken);

            Assert.Equal(0, result.ExitCode);
            Assert.True(result.StandardError.Length == 0, result.StandardError);
            Assert.Equal("Hello, World!", result.StandardOutput.Trim());
        }

        /// <summary>
        /// An If Else between the entry and WriteLine, WriteLine on the True branch (the editor's
        /// smoke flow graph). The condition is set, or left unset. Built directly through
        /// <see cref="SampleProjectFactory"/> (same graph shape as the sample): these tests are about
        /// the translator's behavior, not about loading the checked-in files.
        /// </summary>
        private Project HelloWorldWithIfElse(bool? condition)
        {
            Project project = SampleProjectFactory.CreateHelloWorld(Path.Combine(tempDir, "HelloWorld.netpp"));
            var main = project.Classes.Single().Methods.Single();
            var write = main.Nodes.OfType<NetPrints.Graph.CallMethodNode>().Single();
            var ifElse = new NetPrints.Graph.IfElseNode(main) { PositionX = 280, PositionY = 392 };
            ifElse.ConditionPin.UnconnectedValue = condition;
            NetPrints.Graph.GraphUtil.ConnectExecPins(main.EntryNode.InitialExecutionPin, ifElse.ExecutionPin);
            NetPrints.Graph.GraphUtil.ConnectExecPins(ifElse.TruePin, write.InputExecPins[0]);
            return project;
        }

        [Fact(Timeout = 120000)]
        public async Task IfElseWithConditionCompiles()
        {
            var project = HelloWorldWithIfElse(true);

            await CompileAsync(project, TestContext.Current.CancellationToken);

            Assert.True(project.LastCompilationSucceeded, string.Join(Environment.NewLine, project.LastCompileErrors ?? new ObservableRangeCollection<string>()));
        }

        /// <summary>A graph that cannot be translated fails the build with the translator's message, not with C# syntax errors.</summary>
        [Fact(Timeout = 120000)]
        public async Task UntranslatableGraphReportsTheReason()
        {
            var project = HelloWorldWithIfElse(null);

            await CompileAsync(project, TestContext.Current.CancellationToken);

            Assert.False(project.LastCompilationSucceeded);
            string error = Assert.Single(project.LastCompileErrors);
            Assert.Contains("HelloWorld.Program", error);
            Assert.Contains("Condition", error);
            Assert.Equal("Build failed with 1 error(s)", project.CompilationMessage);
        }

        /// <summary>
        /// An untranslatable class is skipped instead of compiled as its own exception text (which
        /// used to drop every type in the project from search and type pickers, with no
        /// indication why); the reason comes back through the warnings instead.
        /// </summary>
        [Fact(Timeout = 120000)]
        public void UntranslatableGraphIsSkippedNotEmittedAsSource()
        {
            var project = HelloWorldWithIfElse(null);

            var sources = project.GenerateClassSources(out var warnings).ToList();

            Assert.Empty(sources);
            string warning = Assert.Single(warnings);
            Assert.Contains("HelloWorld.Program", warning);
            Assert.Contains("Condition", warning);
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
