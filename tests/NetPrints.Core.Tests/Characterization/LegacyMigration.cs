using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Serialization;
using NetPrints.Serialization.Documents;
using NetPrints.Serialization.Json;
using NetPrints.Serialization.Legacy;
using NetPrints.Serialization.Mapping;
using NetPrints.Serialization.Migrations;
using NetPrints.Tests.Samples;
using Xunit;

namespace NetPrints.Tests.Characterization
{
    /// <summary>
    /// One-time migration of the repository's own legacy fixtures to JSON (tasks.md T054a,
    /// research.md R21): reads each legacy <c>.netpc</c> fixture, rewrites its positional node ids
    /// ("n0", "n1", …) to seeded Snowflake ids, and writes the result with
    /// <see cref="JsonDocumentFormat"/>. Member ids are left as <see cref="LegacyXmlDocumentFormat"/>
    /// assigned them: already <see cref="IdFormat"/>-shaped. Does nothing unless
    /// NETPRINTS_MIGRATE_LEGACY=1; run once and its output committed, this test (and the legacy
    /// importer it is built on) is deleted in T062a.
    /// </summary>
    public class LegacyMigration
    {
        private const string MigrateVariable = "NETPRINTS_MIGRATE_LEGACY";

        [Fact]
        public async Task MigrateLegacyFixturesToJson()
        {
            if (Environment.GetEnvironmentVariable(MigrateVariable) != "1")
            {
                return;
            }

            string root = SampleProjectFactory.FindRepositoryRoot();
            string legacyDir = Path.Combine(root, "tests", "NetPrints.Core.Tests", "Fixtures", "Legacy");

            await MigrateAsync(
                Path.Combine(legacyDir, "AllNodes", "AllNodes.Everything.netpc"),
                "AllNodes", "AllNodes.Everything.netpc.json",
                [Path.Combine(root, "tests", "NetPrints.Core.Tests", "Fixtures", "AllNodes")]);

            await MigrateAsync(
                Path.Combine(legacyDir, "HelloWorld", "HelloWorld.Program.netpc"),
                "HelloWorld", "HelloWorld.Program.netpc.json",
                [
                    Path.Combine(root, "tests", "NetPrints.Core.Tests", "Fixtures", "HelloWorld"),
                    Path.Combine(root, "samples", "HelloWorld"),
                ]);
        }

        /// <summary>
        /// Converts one legacy fixture and writes the migrated JSON into every directory of
        /// <paramref name="destinationDirectories"/>, then reloads the first copy to prove it is
        /// strictly id-shaped and issue-free.
        /// </summary>
        private static async Task MigrateAsync(string legacyPath, string projectName, string documentFileName,
            IReadOnlyList<string> destinationDirectories)
        {
            var registry = new NodeDocumentConverterRegistry(NodeDocumentConverterRegistry.BuiltIn, []);
            var mapper = new DocumentMapper(registry);
            var legacyFormat = new LegacyXmlDocumentFormat(mapper);
            var id = new DocumentId(documentFileName);

            ClassDocument legacyDocument;
            await using (FileStream input = File.OpenRead(legacyPath))
            {
                legacyDocument = await legacyFormat.ReadClassAsync(input, id, TestContext.Current.CancellationToken);
            }

            var issues = new List<DocumentIssue>();
            ClassGraph cls = mapper.FromDocument(legacyDocument, Project.CreateNew(projectName, projectName), issues, id);
            Assert.Empty(issues);

            // Every graph's nodes, in the same deterministic order DocumentMapper itself enumerates
            // them (class graph, then each variable's type/getter/setter graphs, then methods, then
            // constructors), get a fresh seeded id in place of their positional legacy one.
            var nodeIds = new SeededIdGenerator(StableIds.SeedFor(cls.FullName + "/nodes"));
            foreach (NodeGraph graph in EnumerateGraphs(cls))
            {
                foreach (Node node in graph.Nodes)
                {
                    string previousId = node.Id;
                    node.Id = nodeIds.NewId('n');
                    graph.ReindexNode(node, previousId);
                }
            }

            ClassDocument migrated = mapper.ToDocument(cls);
            var jsonFormat = new JsonDocumentFormat(new NetPrintsJsonOptions(registry), new DocumentMigrator([]));

            foreach (string directory in destinationDirectories)
            {
                Directory.CreateDirectory(directory);
                await using FileStream output = File.Create(Path.Combine(directory, documentFileName));
                await jsonFormat.WriteClassAsync(migrated, output, TestContext.Current.CancellationToken);
            }

            await VerifyMigratedDocumentAsync(jsonFormat, mapper, Path.Combine(destinationDirectories[0], documentFileName), projectName, id);
        }

        /// <summary>Reloads a migrated JSON file and asserts it loads with no issues and every node
        /// and member id matches <see cref="IdFormat.Pattern"/>.</summary>
        private static async Task VerifyMigratedDocumentAsync(JsonDocumentFormat jsonFormat, DocumentMapper mapper,
            string writtenPath, string projectName, DocumentId id)
        {
            ClassDocument reloadedDocument;
            await using (FileStream input = File.OpenRead(writtenPath))
            {
                reloadedDocument = await jsonFormat.ReadClassAsync(input, id, TestContext.Current.CancellationToken);
            }

            var issues = new List<DocumentIssue>();
            ClassGraph cls = mapper.FromDocument(reloadedDocument, Project.CreateNew(projectName, projectName), issues, id);
            Assert.Empty(issues);

            foreach (NodeGraph graph in EnumerateGraphs(cls))
            {
                foreach (Node node in graph.Nodes)
                {
                    Assert.Matches(IdFormat.Pattern, node.Id);
                }
            }

            foreach (object member in cls.Members)
            {
                string memberId = member switch
                {
                    Variable variable => variable.Id,
                    MethodGraph method => method.Id,
                    ConstructorGraph constructor => constructor.Id,
                    _ => throw new InvalidOperationException($"Unknown member type '{member.GetType()}'."),
                };
                Assert.Matches(IdFormat.Pattern, memberId);
            }
        }

        /// <summary>
        /// Same graph order as the internal <c>DocumentMapper.EnumerateGraphs</c> (not accessible from
        /// this assembly): the class graph itself, each variable's type/getter/setter graphs, then
        /// methods, then constructors.
        /// </summary>
        private static IEnumerable<NodeGraph> EnumerateGraphs(ClassGraph cls)
        {
            yield return cls;

            foreach (Variable variable in cls.Variables)
            {
                yield return variable.TypeGraph;

                if (variable.GetterMethod is { } getter)
                {
                    yield return getter;
                }

                if (variable.SetterMethod is { } setter)
                {
                    yield return setter;
                }
            }

            foreach (MethodGraph method in cls.Methods)
            {
                yield return method;
            }

            foreach (ConstructorGraph constructor in cls.Constructors)
            {
                yield return constructor;
            }
        }
    }
}
