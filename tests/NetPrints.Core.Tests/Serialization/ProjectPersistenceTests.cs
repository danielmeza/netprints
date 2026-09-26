using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Concurrency;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Core;
using NetPrints.Projects;
using NetPrints.Serialization;
using NetPrints.Serialization.Documents;
using NetPrints.Serialization.Json;
using NetPrints.Serialization.Mapping;
using NetPrints.Serialization.Migrations;
using NetPrints.Serialization.Stores;
using Xunit;

namespace NetPrints.Tests.Serialization
{
    /// <summary>
    /// <see cref="ProjectPersistence"/> (document-format.md §2.8): DF-T11 (a malformed graph file among
    /// others is skipped and reported, the rest of the project loads) and DF-T15 (<c>SaveAsync</c>
    /// writes only dirty classes' graphs and generated C#, each only when its bytes differ from disk).
    /// </summary>
    public sealed class ProjectPersistenceTests : IDisposable
    {
        private readonly string root = Directory.CreateTempSubdirectory("netprints-pp-").FullName;

        public void Dispose()
        {
            try
            { Directory.Delete(root, recursive: true); }
            catch (IOException) { }
        }

        /// <summary><see cref="IProjectSystem"/> stub returning a fixed snapshot; only <see cref="LoadAsync"/> is exercised.</summary>
        private sealed class FakeProjectSystem(ProjectSnapshot snapshot) : IProjectSystem
        {
            public Task<ProjectSnapshot> LoadAsync(string projectFilePath, CancellationToken cancellationToken) =>
                Task.FromResult(snapshot);

            public Task<ProjectSnapshot> ApplyAsync(string projectFilePath, IReadOnlyList<ProjectEdit> edits, CancellationToken cancellationToken) =>
                throw new NotSupportedException();

            public Task<string> CreateAsync(string directory, string projectName, IProjectProfile profile, string rootNamespace, CancellationToken cancellationToken) =>
                throw new NotSupportedException();

            public Task<BuildResult> BuildAsync(string projectFilePath, CancellationToken cancellationToken) =>
                throw new NotSupportedException();

            public ProcessStartRequest GetRunCommand(string projectFilePath) => throw new NotSupportedException();
        }

        private static ProjectSnapshot NewSnapshot(string projectFilePath, IReadOnlyList<string> graphFiles) => new(
            ProjectFilePath: projectFilePath,
            Name: "Test",
            RootNamespace: "Test",
            AssemblyName: "Test",
            OutputType: BinaryType.SharedLibrary,
            TargetFramework: "net10.0",
            ProfileId: "netprints.default",
            ReferencesNetPrintsSdk: true,
            GraphFiles: graphFiles,
            ExtensionFolders: [],
            References: [],
            DeclaredReferences: [],
            OtherSources: [],
            CompilationOptionsJson: "{}",
            Properties: new Dictionary<string, string>(),
            Messages: []);

        private static NodeDocumentConverterRegistry NewRegistry() => new(NodeDocumentConverterRegistry.BuiltIn, []);

        private static JsonDocumentFormat NewJsonFormat(NodeDocumentConverterRegistry registry) =>
            new(new NetPrintsJsonOptions(registry), new DocumentMigrator([]));

        private static ClassDocument MinimalDocument(string name, long nodeIdValue) =>
            new(DocumentMigrator.CurrentSchemaVersion, "Test", name, MemberVisibility.Public, ClassModifiers.None, null,
                new GraphDocument([new ClassReturnNodeDocument(IdFormat.Format('n', nodeIdValue), null, null, 0)], null, null),
                null, null, null, null, null);

        private ProjectPersistence NewPersistence(IProjectSystem projects, DocumentFormatRegistry formats, IDocumentMapper mapper) =>
            new(projects, formats, mapper,
                _ => new FileSystemDocumentStore(root, Scheduler.Default, NullLogger<FileSystemDocumentStore>.Instance),
                NullLogger<ProjectPersistence>.Instance);

        private static async Task WriteFileAsync(JsonDocumentFormat format, ClassDocument document, string path, CancellationToken cancellationToken)
        {
            using var buffer = new MemoryStream();
            await format.WriteClassAsync(document, buffer, cancellationToken);
            await File.WriteAllBytesAsync(path, buffer.ToArray(), cancellationToken);
        }

        // DF-T11: a malformed graph file among others -> the project opens without it, with an issue
        // carrying the document and a line/position (surfaced through the underlying DocumentFormatException).
        [Fact]
        public async Task LoadAsyncSkipsMalformedGraphAndReportsAnIssue()
        {
            CancellationToken ct = TestContext.Current.CancellationToken;
            NodeDocumentConverterRegistry registry = NewRegistry();
            var mapper = new DocumentMapper(registry);
            JsonDocumentFormat jsonFormat = NewJsonFormat(registry);
            var formats = new DocumentFormatRegistry([jsonFormat]);

            string pathA = Path.Combine(root, "A.netpc.json");
            string pathBad = Path.Combine(root, "Bad.netpc.json");
            string pathC = Path.Combine(root, "C.netpc.json");

            await WriteFileAsync(jsonFormat, MinimalDocument("A", 1), pathA, ct);
            await File.WriteAllTextAsync(pathBad, "{\n  \"schemaVersion\": 1,\n  \"name\": ,\n}", ct);
            await WriteFileAsync(jsonFormat, MinimalDocument("C", 2), pathC, ct);

            string csprojPath = Path.Combine(root, "Test.csproj");
            var projects = new FakeProjectSystem(NewSnapshot(csprojPath, [pathA, pathBad, pathC]));
            ProjectPersistence persistence = NewPersistence(projects, formats, mapper);

            ProjectLoadResult result = await persistence.LoadAsync(csprojPath, ct);

            Assert.Equal(2, result.Project.Classes.Count);
            Assert.Contains(result.Project.Classes, c => c.FullName == "Test.A");
            Assert.Contains(result.Project.Classes, c => c.FullName == "Test.C");

            DocumentIssue issue = Assert.Single(result.Issues);
            Assert.Equal(DocumentIssueSeverity.Error, issue.Severity);
            Assert.Equal(DocumentIssue.DocumentUnreadable, issue.Code);
            Assert.Equal(new DocumentId("Bad.netpc.json"), issue.Document);
        }

        // DF-T15: SaveAsync writes only dirty classes' graphs and generated C#, never the clean ones or
        // the .csproj; an unchanged project afterward writes nothing.
        [Fact]
        public async Task SaveAsyncWritesOnlyDirtyClassesAndNothingOnASecondUnchangedSave()
        {
            CancellationToken ct = TestContext.Current.CancellationToken;
            NodeDocumentConverterRegistry registry = NewRegistry();
            var mapper = new DocumentMapper(registry);
            JsonDocumentFormat jsonFormat = NewJsonFormat(registry);
            var formats = new DocumentFormatRegistry([jsonFormat]);
            var projects = new FakeProjectSystem(NewSnapshot(Path.Combine(root, "Test.csproj"), []));
            ProjectPersistence persistence = NewPersistence(projects, formats, mapper);

            Project project = Project.CreateNew("Test", "Test", addDefaultReferences: false);
            project.Path = Path.Combine(root, "Test.csproj");

            ClassGraph dirty = project.CreateNewClass();
            dirty.MarkDirty();
            ClassGraph cleanNoFile = project.CreateNewClass();
            ClassGraph cleanWithFile = project.CreateNewClass();

            string cleanWithFilePath = Path.Combine(root, $"{cleanWithFile.FullName}.netpc.json");
            const string nonCanonicalContent = "// not canonical json\n{}";
            await File.WriteAllTextAsync(cleanWithFilePath, nonCanonicalContent, ct);
            cleanWithFile.LoadedGraphFilePath = cleanWithFilePath;

            Assert.True(dirty.IsDirty);
            Assert.False(cleanNoFile.IsDirty);
            Assert.False(cleanWithFile.IsDirty);

            static string RenderGenerated(ClassGraph cls) => $"// generated for {cls.FullName}\n";

            ProjectSaveResult first = await persistence.SaveAsync(project, RenderGenerated, ct);

            Assert.Equal(2, first.WrittenFiles.Count);

            string dirtyGraphPath = project.GetGraphFilePath(dirty);
            Assert.True(File.Exists(dirtyGraphPath));
            string dirtyGeneratedPath = Path.Combine(root, $"{dirty.FullName}.netpc.g.cs");
            Assert.Equal(RenderGenerated(dirty), await File.ReadAllTextAsync(dirtyGeneratedPath, ct));
            Assert.False(dirty.IsDirty);
            Assert.Equal(dirtyGraphPath, dirty.LoadedGraphFilePath);

            // The clean class's on-disk file (non-canonical) was never touched.
            Assert.Equal(nonCanonicalContent, await File.ReadAllTextAsync(cleanWithFilePath, ct));
            Assert.False(File.Exists(Path.Combine(root, $"{cleanWithFile.FullName}.netpc.g.cs")));

            // The never-saved clean class wrote nothing at all.
            Assert.False(File.Exists(Path.Combine(root, $"{cleanNoFile.FullName}.netpc.json")));

            Assert.False(File.Exists(project.Path));

            ProjectSaveResult second = await persistence.SaveAsync(project, RenderGenerated, ct);
            Assert.Empty(second.WrittenFiles);
        }
    }
}
