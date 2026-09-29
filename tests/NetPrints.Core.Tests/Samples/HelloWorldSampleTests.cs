using System;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Compilation;
using NetPrints.Core;
using NetPrints.Projects;
using NetPrints.Serialization;
using NetPrints.Serialization.Json;
using NetPrints.Serialization.Mapping;
using NetPrints.Serialization.Migrations;
using NetPrints.Serialization.Stores;
using NetPrints.Tests.Projects;
using NetPrints.Translator;
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

        [Fact(Timeout = 120000)]
        public async Task SampleLoadsCompilesAndPrintsHelloWorld()
        {
            CancellationToken cancellationToken = TestContext.Current.CancellationToken;

            SampleBuild sample = SampleBuild.CopyHelloWorld(tempDir);
            MsBuildProjectSystem projectSystem = sample.Projects;
            ProjectPersistence persistence = sample.Persistence;

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
        /// The sample loaded from a temp copy, with an If Else between the entry and WriteLine, WriteLine
        /// on the True branch (the editor's smoke flow graph). The condition is set, or left unset.
        /// These tests are about the translator's behavior, not about loading the checked-in files.
        /// </summary>
        private async Task<(SampleBuild Sample, Project Project)> HelloWorldWithIfElseAsync(bool? condition)
        {
            SampleBuild sample = SampleBuild.CopyHelloWorld(tempDir);
            Project project = await sample.LoadAsync(TestContext.Current.CancellationToken);
            ClassGraph cls = project.Classes.Single();
            var main = cls.Methods.Single();
            var write = main.Nodes.OfType<NetPrints.Graph.CallMethodNode>().Single();
            var ifElse = new NetPrints.Graph.IfElseNode(main) { PositionX = 280, PositionY = 392 };
            ifElse.ConditionPin.UnconnectedValue = condition;
            NetPrints.Graph.GraphUtil.ConnectExecPins(main.EntryNode.InitialExecutionPin, ifElse.ExecutionPin);
            NetPrints.Graph.GraphUtil.ConnectExecPins(ifElse.TruePin, write.InputExecPins[0]);
            cls.MarkDirty();
            return (sample, project);
        }

        [Fact(Timeout = 120000)]
        public async Task IfElseWithConditionCompiles()
        {
            (SampleBuild sample, Project project) = await HelloWorldWithIfElseAsync(true);

            BuildResult build = await sample.SaveAndBuildAsync(project, TestContext.Current.CancellationToken);

            Assert.True(build.Success, build.Log);
        }

        /// <summary>A graph that cannot be translated is reported as a diagnostic carrying the
        /// translator's message (not a C# syntax error) instead of failing the save (R1-01).</summary>
        [Fact(Timeout = 120000)]
        public async Task UntranslatableGraphReportsTheReason()
        {
            (SampleBuild sample, Project project) = await HelloWorldWithIfElseAsync(null);

            ProjectSaveResult result = await sample.Persistence.SaveAsync(
                project, cls => SampleBuild.Render(project, cls), TestContext.Current.CancellationToken);

            CodeDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Contains("Condition", diagnostic.Message);
        }

        /// <summary>
        /// An untranslatable class is skipped instead of compiled as its own exception text (which
        /// used to drop every type in the project from search and type pickers, with no
        /// indication why); the reason comes back as an <c>NPT</c> diagnostic instead (R1-16: the same
        /// <see cref="ProjectTranslation.TranslateAll"/> path <c>CodeAnalysisHost</c> and
        /// <c>ProjectCheck</c> use, not the deleted <c>Project.GenerateClassSources</c>).
        /// </summary>
        [Fact(Timeout = 120000)]
        public async Task UntranslatableGraphIsSkippedNotEmittedAsSource()
        {
            (_, Project project) = await HelloWorldWithIfElseAsync(null);

            ProjectTranslationResult result = ProjectTranslation.TranslateAll(project, TranslationEnvironment.BuiltIn);

            Assert.Empty(result.Classes);
            CodeDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("HelloWorld.Program", diagnostic.ClassFullName);
            Assert.Contains("Condition", diagnostic.Message);
        }
    }
}
