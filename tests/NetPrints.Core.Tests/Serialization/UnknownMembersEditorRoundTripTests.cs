using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Text;
using System.Text.Json.Nodes;
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
using NetPrints.Tests.Samples;
using Xunit;

namespace NetPrints.Tests.Serialization
{
    /// <summary>
    /// FR-007 (AB-1): members this build does not know survive the editor's path, a load through
    /// <see cref="ProjectPersistence"/> and a save of a dirty class, byte for byte.
    /// </summary>
    public sealed class UnknownMembersEditorRoundTripTests : IDisposable
    {
        private const string Marker = "zzFuture";

        private readonly string root = Directory.CreateTempSubdirectory("netprints-unknown-").FullName;

        public void Dispose()
        {
            try
            { Directory.Delete(root, recursive: true); }
            catch (IOException) { }
        }

        public static TheoryData<string> Fixtures => new()
        {
            "HelloWorld/HelloWorld.Program.netpc.json",
            "AllNodes/AllNodes.Everything.netpc.json",
            "Locals/Locals.netpc.json",
            "EventGraphs/EventGraphs.GameEvents.netpc.json",
        };

        [Theory]
        [MemberData(nameof(Fixtures))]
        public async Task UnknownMembersOnEveryDocumentRecordSurviveOpenAndSave(string fixture)
        {
            CancellationToken ct = TestContext.Current.CancellationToken;
            NodeDocumentConverterRegistry registry = new(NodeDocumentConverterRegistry.BuiltIn, []);
            JsonDocumentFormat format = new(new NetPrintsJsonOptions(registry), new DocumentMigrator([], NullLogger<DocumentMigrator>.Instance));

            string source = Path.Combine(SampleProjectFactory.FindRepositoryRoot(), "tests", "NetPrints.Core.Tests", "Fixtures", fixture);
            JsonObject decorated = JsonNode.Parse(await File.ReadAllTextAsync(source, ct))?.AsObject()
                ?? throw new InvalidOperationException("The fixture is not a JSON object.");
            Decorate(decorated, "class");

            ClassDocument document;
            using (var input = new MemoryStream(Encoding.UTF8.GetBytes(decorated.ToJsonString())))
            {
                document = await format.ReadClassAsync(input, new DocumentId("a.netpc.json"), ct);
            }

            string graphPath = Path.Combine(root, "A.netpc.json");
            byte[] expected;
            using (var buffer = new MemoryStream())
            {
                await format.WriteClassAsync(document, buffer, ct);
                expected = buffer.ToArray();
            }

            await File.WriteAllBytesAsync(graphPath, expected, ct);
            Assert.Contains(Marker, Encoding.UTF8.GetString(expected), StringComparison.Ordinal);

            string projectPath = Path.Combine(root, "Test.csproj");
            var mapper = new DocumentMapper(registry, NullLogger<DocumentMapper>.Instance);
            var persistence = new ProjectPersistence(
                new FakeProjectSystem(NewSnapshot(projectPath, [graphPath])),
                new DocumentFormatRegistry([format]),
                mapper,
                (directory, watch) => new FileSystemDocumentStore(directory, Scheduler.Default, NullLogger<FileSystemDocumentStore>.Instance, watch),
                NullLogger<ProjectPersistence>.Instance);

            ProjectLoadResult loaded = await persistence.LoadAsync(projectPath, ct);
            foreach (ClassGraph cls in loaded.Project.Classes)
            {
                cls.MarkDirty();
            }

            File.Delete(graphPath);
            await persistence.SaveAsync(loaded.Project, _ => "// generated\n", ct);

            Assert.Equal(Encoding.UTF8.GetString(expected), await File.ReadAllTextAsync(graphPath, ct));
        }

        // Adds one unknown member to every JSON object that is a document record (not a reference), under the path that names it.
        private static void Decorate(JsonObject record, string path)
        {
            record[Marker + path.Replace('/', '_').Replace('[', '_').Replace(']', '_')] = path;

            foreach (string name in new[] { "classGraph", "graph", "typeGraph", "getter", "setter" })
            {
                if (record[name] is JsonObject child)
                {
                    Decorate(child, path + "/" + name);
                }
            }

            foreach (string name in new[] { "variables", "methods", "constructors", "eventGraphs", "nodes", "pins", "connections", "locals" })
            {
                if (record[name] is JsonArray items)
                {
                    for (int i = 0; i < items.Count; i++)
                    {
                        if (items[i] is JsonObject item)
                        {
                            Decorate(item, $"{path}/{name}[{i}]");
                        }
                    }
                }
            }
        }

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
    }
}
