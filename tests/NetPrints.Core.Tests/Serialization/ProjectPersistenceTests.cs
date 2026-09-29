using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Compilation;
using NetPrints.Core;
using NetPrints.Projects;
using NetPrints.Serialization;
using NetPrints.Serialization.Documents;
using NetPrints.Serialization.Json;
using NetPrints.Serialization.Mapping;
using NetPrints.Serialization.Migrations;
using NetPrints.Serialization.Stores;
using NetPrints.Translator;
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
            new(new NetPrintsJsonOptions(registry), new DocumentMigrator([], NullLogger<DocumentMigrator>.Instance));

        private static ClassDocument MinimalDocument(string name, long nodeIdValue) =>
            new(DocumentMigrator.CurrentSchemaVersion, "Test", name, MemberVisibility.Public, ClassModifiers.None, null,
                new GraphDocument([new ClassReturnNodeDocument(IdFormat.Format('n', nodeIdValue), null, null, 0)], null, null),
                null, null, null, null, null);

        // R1-08 (issues case): a raw, not-IdFormat-shaped id reports NPD009 (InvalidIdReassigned) but
        // still loads, so AddGraphAsync has something non-fatal to return in its issues list.
        private static ClassDocument DocumentWithInvalidNodeId(string name) =>
            new(DocumentMigrator.CurrentSchemaVersion, "Test", name, MemberVisibility.Public, ClassModifiers.None, null,
                new GraphDocument([new ClassReturnNodeDocument("not-a-valid-id", null, null, 0)], null, null),
                null, null, null, null, null);

        private ProjectPersistence NewPersistence(IProjectSystem projects, DocumentFormatRegistry formats, IDocumentMapper mapper) =>
            new(projects, formats, mapper,
                (_, watch) => new FileSystemDocumentStore(root, Scheduler.Default, NullLogger<FileSystemDocumentStore>.Instance, watch),
                NullLogger<ProjectPersistence>.Instance);

        // R1-06: unlike NewPersistence above (always rooted at the test's temp dir), this respects
        // whatever directory ProjectPersistence itself passes to createStore — exactly like the real
        // callers (EditorComposition, ProjectCheck) — so a store rooted wider than the project directory
        // (a graph outside it) is actually exercised end to end.
        private static ProjectPersistence NewPersistenceAtGivenDirectory(IProjectSystem projects, DocumentFormatRegistry formats, IDocumentMapper mapper) =>
            new(projects, formats, mapper,
                (directory, watch) => new FileSystemDocumentStore(directory, Scheduler.Default, NullLogger<FileSystemDocumentStore>.Instance, watch),
                NullLogger<ProjectPersistence>.Instance);

        // R1-09: every load, save or add must ask for a non-watching store — a plain read/write never
        // needs a recursive FileSystemWatcher over the project directory.
        [Fact]
        public async Task LoadSaveAndAddGraphAllRequestANonWatchingStore()
        {
            CancellationToken ct = TestContext.Current.CancellationToken;
            var requestedWatch = new List<bool>();
            NodeDocumentConverterRegistry registry = NewRegistry();
            var mapper = new DocumentMapper(registry, NullLogger<DocumentMapper>.Instance);
            JsonDocumentFormat jsonFormat = NewJsonFormat(registry);
            var formats = new DocumentFormatRegistry([jsonFormat]);
            string projectPath = Path.Combine(root, "Test.csproj");
            string graphPath = Path.Combine(root, "A.netpc.json");
            await WriteFileAsync(jsonFormat, MinimalDocument("A", 1), graphPath, ct);
            var projects = new FakeProjectSystem(NewSnapshot(projectPath, [graphPath]));
            var persistence = new ProjectPersistence(projects, formats, mapper,
                (directory, watch) =>
                {
                    requestedWatch.Add(watch);
                    return new FileSystemDocumentStore(directory, Scheduler.Default, NullLogger<FileSystemDocumentStore>.Instance, watch);
                },
                NullLogger<ProjectPersistence>.Instance);

            ProjectLoadResult loaded = await persistence.LoadAsync(projectPath, ct);
            await persistence.SaveAsync(loaded.Project, _ => "// generated\n", ct);

            string sourceDir = Path.Combine(root, "Source");
            Directory.CreateDirectory(sourceDir);
            string sourceGraphPath = Path.Combine(sourceDir, "B.netpc.json");
            await WriteFileAsync(jsonFormat, MinimalDocument("B", 2), sourceGraphPath, ct);
            await persistence.AddGraphAsync(loaded.Project, sourceGraphPath, ct);

            Assert.NotEmpty(requestedWatch);
            Assert.All(requestedWatch, Assert.False);
        }

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
            var mapper = new DocumentMapper(registry, NullLogger<DocumentMapper>.Instance);
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

        // T075: a host that evaluated the project itself (to decide which extensions to load) hands the snapshot over.
        [Fact]
        public async Task LoadAsyncFromASnapshotDoesNotEvaluateTheProjectAgain()
        {
            CancellationToken ct = TestContext.Current.CancellationToken;
            NodeDocumentConverterRegistry registry = NewRegistry();
            JsonDocumentFormat jsonFormat = NewJsonFormat(registry);
            string pathA = Path.Combine(root, "A.netpc.json");
            await WriteFileAsync(jsonFormat, MinimalDocument("A", 1), pathA, ct);
            ProjectSnapshot snapshot = NewSnapshot(Path.Combine(root, "Test.csproj"), [pathA]);
            ProjectPersistence persistence = NewPersistence(new ThrowingProjectSystem(), new DocumentFormatRegistry([jsonFormat]), new DocumentMapper(registry, NullLogger<DocumentMapper>.Instance));

            ProjectLoadResult result = await persistence.LoadAsync(snapshot, ct);

            Assert.Same(snapshot, result.Snapshot);
            Assert.Equal("Test.A", Assert.Single(result.Project.Classes).FullName);
            Assert.Empty(result.Issues);
        }

        private sealed class ThrowingProjectSystem : IProjectSystem
        {
            public Task<ProjectSnapshot> LoadAsync(string projectFilePath, CancellationToken cancellationToken) => throw new NotSupportedException();

            public Task<ProjectSnapshot> ApplyAsync(string projectFilePath, IReadOnlyList<ProjectEdit> edits, CancellationToken cancellationToken) =>
                throw new NotSupportedException();

            public Task<string> CreateAsync(string directory, string projectName, IProjectProfile profile, string rootNamespace, CancellationToken cancellationToken) =>
                throw new NotSupportedException();

            public Task<BuildResult> BuildAsync(string projectFilePath, CancellationToken cancellationToken) => throw new NotSupportedException();

            public ProcessStartRequest GetRunCommand(string projectFilePath) => throw new NotSupportedException();
        }

        // DF-T15: SaveAsync writes only dirty classes' graphs and generated C#, never the clean ones or
        // the .csproj; an unchanged project afterward writes nothing.
        [Fact]
        public async Task SaveAsyncWritesOnlyDirtyClassesAndNothingOnASecondUnchangedSave()
        {
            CancellationToken ct = TestContext.Current.CancellationToken;
            NodeDocumentConverterRegistry registry = NewRegistry();
            var mapper = new DocumentMapper(registry, NullLogger<DocumentMapper>.Instance);
            JsonDocumentFormat jsonFormat = NewJsonFormat(registry);
            var formats = new DocumentFormatRegistry([jsonFormat]);
            var projects = new FakeProjectSystem(NewSnapshot(Path.Combine(root, "Test.csproj"), []));
            ProjectPersistence persistence = NewPersistence(projects, formats, mapper);

            Project project = TestProjects.Create("Test", "Test", Path.Combine(root, "Test.csproj"));

            IProjectProfile profile = DefaultProjectProfile.Instance;
            ClassGraph dirty = project.CreateNewClass(profile);
            ClassGraph cleanNoFile = project.CreateNewClass(profile);
            cleanNoFile.MarkClean();
            ClassGraph cleanWithFile = project.CreateNewClass(profile);
            cleanWithFile.MarkClean();

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

        // R1-01: a translation failure while rendering one dirty class's generated C# must not abort
        // the save loop. The graph JSON is written for every dirty class regardless, the failing
        // class's previous .g.cs is left alone, and later dirty classes still get theirs written.
        [Fact]
        public async Task SaveAsyncIsolatesATranslationFailureAndStillSavesLaterDirtyClasses()
        {
            CancellationToken ct = TestContext.Current.CancellationToken;
            NodeDocumentConverterRegistry registry = NewRegistry();
            var mapper = new DocumentMapper(registry, NullLogger<DocumentMapper>.Instance);
            JsonDocumentFormat jsonFormat = NewJsonFormat(registry);
            var formats = new DocumentFormatRegistry([jsonFormat]);
            var projects = new FakeProjectSystem(NewSnapshot(Path.Combine(root, "Test.csproj"), []));
            ProjectPersistence persistence = NewPersistence(projects, formats, mapper);

            Project project = TestProjects.Create("Test", "Test", Path.Combine(root, "Test.csproj"));
            IProjectProfile profile = DefaultProjectProfile.Instance;
            ClassGraph failing = project.CreateNewClass(profile);
            ClassGraph ok = project.CreateNewClass(profile);

            Assert.True(failing.IsDirty);
            Assert.True(ok.IsDirty);

            string RenderGenerated(ClassGraph cls) => cls == failing
                ? throw new TranslationException(TranslationDiagnosticCodes.UnsetRequiredInput, "boom", "graphKey", "n1")
                : $"// generated for {cls.FullName}\n";

            ProjectSaveResult result = await persistence.SaveAsync(project, RenderGenerated, ct);

            // Both graphs are written even though the first class's render threw.
            Assert.True(File.Exists(project.GetGraphFilePath(failing)));
            Assert.True(File.Exists(project.GetGraphFilePath(ok)));

            // The failing class's .g.cs was never written; the class after it still got its written
            // (this is the regression: before the fix, the exception aborted the whole loop).
            Assert.False(File.Exists(Path.Combine(root, $"{failing.FullName}.netpc.g.cs")));
            string okGeneratedPath = Path.Combine(root, $"{ok.FullName}.netpc.g.cs");
            Assert.Equal(RenderGenerated(ok), await File.ReadAllTextAsync(okGeneratedPath, ct));

            Assert.False(failing.IsDirty);
            Assert.False(ok.IsDirty);

            CodeDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal(TranslationDiagnosticCodes.UnsetRequiredInput, diagnostic.Id);
            Assert.Equal(failing.FullName, diagnostic.ClassFullName);
        }

        // F-07: a raw exception from renderGenerated that is not a TranslationException (e.g. the
        // InvalidOperationException ExecutionGraphTranslator throws for an unresolved pin type, or
        // anything an extension translator throws) must be isolated the same way, as NPT000.
        [Fact]
        public async Task SaveAsyncIsolatesANonTranslationExceptionAndStillSavesLaterDirtyClasses()
        {
            CancellationToken ct = TestContext.Current.CancellationToken;
            NodeDocumentConverterRegistry registry = NewRegistry();
            var mapper = new DocumentMapper(registry, NullLogger<DocumentMapper>.Instance);
            JsonDocumentFormat jsonFormat = NewJsonFormat(registry);
            var formats = new DocumentFormatRegistry([jsonFormat]);
            var projects = new FakeProjectSystem(NewSnapshot(Path.Combine(root, "Test.csproj"), []));
            ProjectPersistence persistence = NewPersistence(projects, formats, mapper);

            Project project = TestProjects.Create("Test", "Test", Path.Combine(root, "Test.csproj"));
            IProjectProfile profile = DefaultProjectProfile.Instance;
            ClassGraph failing = project.CreateNewClass(profile);
            ClassGraph ok = project.CreateNewClass(profile);

            Assert.True(failing.IsDirty);
            Assert.True(ok.IsDirty);

            string RenderGenerated(ClassGraph cls) => cls == failing
                ? throw new InvalidOperationException("The type of pin 'x' on 'y' is not resolved.")
                : $"// generated for {cls.FullName}\n";

            ProjectSaveResult result = await persistence.SaveAsync(project, RenderGenerated, ct);

            // Both graphs are written even though the first class's render threw a raw exception.
            Assert.True(File.Exists(project.GetGraphFilePath(failing)));
            Assert.True(File.Exists(project.GetGraphFilePath(ok)));

            Assert.False(File.Exists(Path.Combine(root, $"{failing.FullName}.netpc.g.cs")));
            string okGeneratedPath = Path.Combine(root, $"{ok.FullName}.netpc.g.cs");
            Assert.Equal(RenderGenerated(ok), await File.ReadAllTextAsync(okGeneratedPath, ct));

            Assert.False(failing.IsDirty);
            Assert.False(ok.IsDirty);

            CodeDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal(TranslationDiagnosticCodes.Unclassified, diagnostic.Id);
            Assert.Equal(failing.FullName, diagnostic.ClassFullName);
        }

        // R1-06: a graph outside the project directory (project-system.md §7's
        // <NetPrintsGraph Include="../Shared/X.netpc.json" />) must load instead of LoadAsync throwing
        // ArgumentException from FileSystemDocumentStore.ToDocumentId.
        [Fact]
        public async Task LoadAsyncLoadsAGraphOutsideTheProjectDirectory()
        {
            CancellationToken ct = TestContext.Current.CancellationToken;
            NodeDocumentConverterRegistry registry = NewRegistry();
            var mapper = new DocumentMapper(registry, NullLogger<DocumentMapper>.Instance);
            JsonDocumentFormat jsonFormat = NewJsonFormat(registry);
            var formats = new DocumentFormatRegistry([jsonFormat]);

            string appDir = Path.Combine(root, "App");
            string sharedDir = Path.Combine(root, "Shared");
            Directory.CreateDirectory(appDir);
            Directory.CreateDirectory(sharedDir);

            string pathInProject = Path.Combine(appDir, "A.netpc.json");
            string pathOutsideProject = Path.Combine(sharedDir, "Out.netpc.json");
            await WriteFileAsync(jsonFormat, MinimalDocument("A", 1), pathInProject, ct);
            await WriteFileAsync(jsonFormat, MinimalDocument("Out", 2), pathOutsideProject, ct);

            string csprojPath = Path.Combine(appDir, "Test.csproj");
            var projects = new FakeProjectSystem(NewSnapshot(csprojPath, [pathInProject, pathOutsideProject]));
            ProjectPersistence persistence = NewPersistenceAtGivenDirectory(projects, formats, mapper);

            ProjectLoadResult result = await persistence.LoadAsync(csprojPath, ct);

            Assert.Empty(result.Issues);
            Assert.Equal(2, result.Project.Classes.Count);
            Assert.Contains(result.Project.Classes, c => c.FullName == "Test.A");
            Assert.Contains(result.Project.Classes, c => c.FullName == "Test.Out");
        }

        // R1-08, case 1: reading and mapping the source succeeds, the class is added, and any non-fatal
        // mapping issues (previously discarded) are returned instead.
        [Fact]
        public async Task AddGraphAsyncAddsTheClassAndReturnsMappingIssues()
        {
            CancellationToken ct = TestContext.Current.CancellationToken;
            NodeDocumentConverterRegistry registry = NewRegistry();
            var mapper = new DocumentMapper(registry, NullLogger<DocumentMapper>.Instance);
            JsonDocumentFormat jsonFormat = NewJsonFormat(registry);
            var formats = new DocumentFormatRegistry([jsonFormat]);
            var projects = new FakeProjectSystem(NewSnapshot(Path.Combine(root, "Test.csproj"), []));
            ProjectPersistence persistence = NewPersistence(projects, formats, mapper);

            Project project = TestProjects.Create("Test", "Test", Path.Combine(root, "Test.csproj"));
            string sourceDir = Path.Combine(root, "Source");
            Directory.CreateDirectory(sourceDir);
            string sourcePath = Path.Combine(sourceDir, "C.netpc.json");
            await WriteFileAsync(jsonFormat, DocumentWithInvalidNodeId("C"), sourcePath, ct);

            (ClassGraph added, IReadOnlyList<DocumentIssue> issues) = await persistence.AddGraphAsync(project, sourcePath, ct);

            Assert.Equal("Test.C", added.FullName);
            DocumentIssue issue = Assert.Single(issues);
            Assert.Equal(DocumentIssue.InvalidIdReassigned, issue.Code);
            Assert.Contains(project.Classes, c => c.FullName == "Test.C");
            Assert.True(File.Exists(Path.Combine(root, "C.netpc.json")));
        }

        // R1-08, case 2: an existing target file is refused, not silently overwritten, and nothing is
        // added to the project.
        [Fact]
        public async Task AddGraphAsyncRefusesWhenTheTargetFileAlreadyExists()
        {
            CancellationToken ct = TestContext.Current.CancellationToken;
            NodeDocumentConverterRegistry registry = NewRegistry();
            var mapper = new DocumentMapper(registry, NullLogger<DocumentMapper>.Instance);
            JsonDocumentFormat jsonFormat = NewJsonFormat(registry);
            var formats = new DocumentFormatRegistry([jsonFormat]);
            var projects = new FakeProjectSystem(NewSnapshot(Path.Combine(root, "Test.csproj"), []));
            ProjectPersistence persistence = NewPersistence(projects, formats, mapper);

            Project project = TestProjects.Create("Test", "Test", Path.Combine(root, "Test.csproj"));
            string existingTargetPath = Path.Combine(root, "B.netpc.json");
            const string existingContent = "existing content, must be untouched";
            await File.WriteAllTextAsync(existingTargetPath, existingContent, ct);

            string sourceDir = Path.Combine(root, "Source");
            Directory.CreateDirectory(sourceDir);
            string sourcePath = Path.Combine(sourceDir, "B.netpc.json");
            await WriteFileAsync(jsonFormat, MinimalDocument("B", 1), sourcePath, ct);

            await Assert.ThrowsAsync<InvalidOperationException>(() => persistence.AddGraphAsync(project, sourcePath, ct));

            Assert.Equal(existingContent, await File.ReadAllTextAsync(existingTargetPath, ct));
            Assert.Empty(project.Classes);
        }

        // R1-08, case 3: a class with the same FullName already loaded is refused before anything is
        // copied or re-added as a duplicate.
        [Fact]
        public async Task AddGraphAsyncRefusesADuplicateFullNameAlreadyInTheProject()
        {
            CancellationToken ct = TestContext.Current.CancellationToken;
            NodeDocumentConverterRegistry registry = NewRegistry();
            var mapper = new DocumentMapper(registry, NullLogger<DocumentMapper>.Instance);
            JsonDocumentFormat jsonFormat = NewJsonFormat(registry);
            var formats = new DocumentFormatRegistry([jsonFormat]);
            var projects = new FakeProjectSystem(NewSnapshot(Path.Combine(root, "Test.csproj"), []));
            ProjectPersistence persistence = NewPersistence(projects, formats, mapper);

            Project project = TestProjects.Create("Test", "Test", Path.Combine(root, "Test.csproj"));
            var existingIssues = new List<DocumentIssue>();
            ClassGraph existing = mapper.FromDocument(MinimalDocument("A", 1), project, existingIssues, new DocumentId("A.netpc.json"));
            project.Classes.Add(existing);

            string sourceDir = Path.Combine(root, "Source");
            Directory.CreateDirectory(sourceDir);
            string sourcePath = Path.Combine(sourceDir, "A.netpc.json");
            await WriteFileAsync(jsonFormat, MinimalDocument("A", 2), sourcePath, ct);

            await Assert.ThrowsAsync<InvalidOperationException>(() => persistence.AddGraphAsync(project, sourcePath, ct));

            Assert.False(File.Exists(Path.Combine(root, "A.netpc.json")));
            Assert.Single(project.Classes);
        }

        // R1-08, case 4: a malformed source is never copied into the project folder (read-then-copy, not
        // copy-then-read).
        [Fact]
        public async Task AddGraphAsyncDoesNotCopyAMalformedSourceIntoTheProject()
        {
            CancellationToken ct = TestContext.Current.CancellationToken;
            NodeDocumentConverterRegistry registry = NewRegistry();
            var mapper = new DocumentMapper(registry, NullLogger<DocumentMapper>.Instance);
            JsonDocumentFormat jsonFormat = NewJsonFormat(registry);
            var formats = new DocumentFormatRegistry([jsonFormat]);
            var projects = new FakeProjectSystem(NewSnapshot(Path.Combine(root, "Test.csproj"), []));
            ProjectPersistence persistence = NewPersistence(projects, formats, mapper);

            Project project = TestProjects.Create("Test", "Test", Path.Combine(root, "Test.csproj"));
            string sourceDir = Path.Combine(root, "Source");
            Directory.CreateDirectory(sourceDir);
            string sourcePath = Path.Combine(sourceDir, "Bad.netpc.json");
            await File.WriteAllTextAsync(sourcePath, "{\n  \"schemaVersion\": 1,\n  \"name\": ,\n}", ct);

            await Assert.ThrowsAsync<DocumentFormatException>(() => persistence.AddGraphAsync(project, sourcePath, ct));

            Assert.False(File.Exists(Path.Combine(root, "Bad.netpc.json")));
            Assert.Empty(project.Classes);
        }
    }
}
