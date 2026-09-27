using System;
using System.IO;
using System.Reactive.Concurrency;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Core;
using NetPrints.Generator;
using NetPrints.Projects;
using NetPrints.Serialization;
using NetPrints.Serialization.Json;
using NetPrints.Serialization.Mapping;
using NetPrints.Serialization.Migrations;
using NetPrints.Serialization.Stores;
using NetPrints.Tests.Projects;
using NetPrints.Translator;
using NetPrints.Workspace;

namespace NetPrints.Tests.Samples
{
    /// <summary>
    /// The editor's save-and-build path (<c>MainEditorVM.CompileAsync</c>) for tests: a temp copy of the
    /// checked-in <c>samples/HelloWorld</c> project, loaded and saved through
    /// <see cref="ProjectPersistence"/> and built through <see cref="IProjectSystem"/>.
    /// </summary>
    internal sealed class SampleBuild
    {
        private SampleBuild(string directory)
        {
            Directory = directory;
            ProjectFile = Path.Combine(directory, "HelloWorld.csproj");
            Projects = new MsBuildProjectSystem(new ProjectSystemOptions([], "1.0.0-test"), new ProcessRunner(), NullLogger<MsBuildProjectSystem>.Instance);
            Persistence = NewPersistence(Projects);
        }

        public string Directory { get; }

        public string ProjectFile { get; }

        public MsBuildProjectSystem Projects { get; }

        public ProjectPersistence Persistence { get; }

        /// <summary>Copies <c>samples/HelloWorld</c> (linked into the test output) into <paramref name="directory"/> and adds the local SDK layout.</summary>
        public static SampleBuild CopyHelloWorld(string directory)
        {
            string source = Path.Combine(AppContext.BaseDirectory, "samples", "HelloWorld");
            foreach (string file in System.IO.Directory.GetFiles(source))
            {
                File.Copy(file, Path.Combine(directory, Path.GetFileName(file)));
            }

            LocalSdkLayout.Write(directory);
            return new SampleBuild(directory);
        }

        public static ProjectPersistence NewPersistence(IProjectSystem projects)
        {
            var registry = new NodeDocumentConverterRegistry(NodeDocumentConverterRegistry.BuiltIn, []);
            var mapper = new DocumentMapper(registry);
            var formats = new DocumentFormatRegistry([new JsonDocumentFormat(new NetPrintsJsonOptions(registry), new DocumentMigrator([]))]);
            return new ProjectPersistence(projects, formats, mapper,
                dir => new FileSystemDocumentStore(dir, Scheduler.Default, NullLogger<FileSystemDocumentStore>.Instance),
                NullLogger<ProjectPersistence>.Instance);
        }

        /// <summary>Renders <paramref name="cls"/>'s generated file, as the editor does before a build.</summary>
        public static string Render(Project project, ClassGraph cls) =>
            GraphCodeGenerator.RenderFile(new ClassTranslator(TranslationEnvironment.BuiltIn).TranslateClass(cls), Path.GetFileName(project.GetGraphFilePath(cls)));

        public async Task<Project> LoadAsync(CancellationToken cancellationToken)
        {
            ProjectLoadResult loaded = await Persistence.LoadAsync(ProjectFile, cancellationToken);
            return loaded.Project;
        }

        /// <summary>Saves the project's dirty classes, then builds it.</summary>
        public async Task<BuildResult> SaveAndBuildAsync(Project project, CancellationToken cancellationToken)
        {
            await Persistence.SaveAsync(project, cls => Render(project, cls), cancellationToken);
            return await Projects.BuildAsync(ProjectFile, cancellationToken);
        }
    }
}
