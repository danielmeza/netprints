using System;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Reactive.Testing;
using NetPrints.Core;
using NetPrints.Serialization;
using NetPrints.Serialization.Documents;
using NetPrints.Serialization.Mapping;
using NetPrints.Serialization.Migrations;
using NetPrints.Serialization.Stores;
using NetPrints.Tests.Extensibility;
using Xunit;

namespace NetPrints.Tests.Serialization
{
    /// <summary>
    /// T103: the serialization log call sites of editor-services.md §6 (3001-3003, 3005; 3004 and 3006
    /// were retired by research.md R21), each exercised with a collecting logger.
    /// </summary>
    public class LoggingTests
    {
        private sealed class BumpSchemaVersionMigration : IDocumentMigration
        {
            public DocumentKind Kind => DocumentKind.Class;
            public int FromVersion => 1;
            public void Migrate(JsonObject document) => document["schemaVersion"] = 2;
        }

        // 3001: DocumentMigrated is logged once a migration actually runs, with the from/to versions.
        [Fact]
        public void DocumentMigratorLogs3001WhenAMigrationRuns()
        {
            var factory = new CollectingLoggerFactory();
            var migrator = new DocumentMigrator([new BumpSchemaVersionMigration()], factory.CreateLogger<DocumentMigrator>());
            var document = new JsonObject { ["schemaVersion"] = 1 };
            var id = new DocumentId("a.netpc.json");

            migrator.Upgrade(document, DocumentKind.Class, id);

            var entry = Assert.Single(factory.Entries);
            Assert.Equal(3001, entry.EventId.Id);
            Assert.Equal(LogLevel.Information, entry.Level);
            Assert.Contains(id.ToString(), entry.Message, StringComparison.Ordinal);
        }

        // No migration ran (the document is already at Supported): nothing is logged.
        [Fact]
        public void DocumentMigratorLogsNothingWhenNoMigrationRuns()
        {
            var factory = new CollectingLoggerFactory();
            var migrator = new DocumentMigrator([], factory.CreateLogger<DocumentMigrator>());
            var document = new JsonObject { ["schemaVersion"] = 1 };

            migrator.Upgrade(document, DocumentKind.Class, new DocumentId("a.netpc.json"));

            Assert.Empty(factory.Entries);
        }

        // 3002: UnknownNodeKindPreserved is logged alongside the UnknownNodeKind issue.
        [Fact]
        public void DocumentMapperLogs3002ForAnUnknownNodeKind()
        {
            var factory = new CollectingLoggerFactory();
            var registry = new NodeDocumentConverterRegistry(NodeDocumentConverterRegistry.BuiltIn, []);
            var mapper = new DocumentMapper(registry, factory.CreateLogger<DocumentMapper>());

            JsonElement raw = JsonDocument.Parse("""{"$kind":"test.ext/widget","id":"n9","extra":1}""").RootElement;
            var unknownNode = new UnknownNodeDocument("n9", "test.ext/widget", raw);
            var classReturn = new ClassReturnNodeDocument(IdFormat.Format('n', 0), null, null, 0);
            var classGraph = new GraphDocument([classReturn, unknownNode], null, null);
            var classDocument = new ClassDocument(1, null, "C", MemberVisibility.Public, ClassModifiers.None, null,
                classGraph, null, null, null, null, null);

            var issues = new System.Collections.Generic.List<DocumentIssue>();
            mapper.FromDocument(classDocument, TestProjects.Create("P", "P"), issues, new DocumentId("C.netpc.json"));

            var entry = Assert.Single(factory.Entries);
            Assert.Equal(3002, entry.EventId.Id);
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.Contains("n9", entry.Message, StringComparison.Ordinal);
            Assert.Contains("test.ext/widget", entry.Message, StringComparison.Ordinal);
        }

        // 3003: ConnectionDropped is logged alongside the ConnectionDropped issue.
        [Fact]
        public void DocumentMapperLogs3003ForADroppedConnection()
        {
            var factory = new CollectingLoggerFactory();
            var registry = new NodeDocumentConverterRegistry(NodeDocumentConverterRegistry.BuiltIn, []);
            var mapper = new DocumentMapper(registry, factory.CreateLogger<DocumentMapper>());

            string returnId = IdFormat.Format('n', 0);
            var classReturn = new ClassReturnNodeDocument(returnId, null, null, 0);
            var connections = new System.Collections.Generic.List<ConnectionDocument>
            {
                new("missing/out.type.Whatever", $"{returnId}/in.type.BaseType"),
            };
            var classGraph = new GraphDocument([classReturn], connections, null);
            var classDocument = new ClassDocument(1, null, "C", MemberVisibility.Public, ClassModifiers.None, null,
                classGraph, null, null, null, null, null);

            var issues = new System.Collections.Generic.List<DocumentIssue>();
            mapper.FromDocument(classDocument, TestProjects.Create("P", "P"), issues, new DocumentId("C.netpc.json"));

            Assert.Contains(issues, i => i.Code == DocumentIssue.ConnectionDropped);
            var entry = Assert.Single(factory.Entries);
            Assert.Equal(3003, entry.EventId.Id);
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.Contains("missing", entry.Message, StringComparison.Ordinal);
        }

        private static async Task PumpAsync(TestScheduler scheduler, Func<bool> done)
        {
            for (int i = 0; i < 150 && !done(); i++)
            {
                await Task.Delay(20, TestContext.Current.CancellationToken);
                scheduler.AdvanceBy(TimeSpan.FromMilliseconds(20).Ticks);
            }
        }

        // 3005: ExternalChange is logged for a change that bypasses the store (DF-T13's own scenario,
        // isolated here with a collecting logger instead of NullLogger).
        [Fact]
        public async Task FileSystemDocumentStoreLogs3005ForAnExternalChange()
        {
            string directory = Path.Combine(Path.GetTempPath(), "netprints-logging-" + Guid.NewGuid().ToString("N"));
            var scheduler = new TestScheduler();
            var factory = new CollectingLoggerFactory();
            using var store = new FileSystemDocumentStore(directory, scheduler, factory.CreateLogger<FileSystemDocumentStore>());
            var id = new DocumentId("a.txt");

            await store.WriteAsync(id, (s, ct) => new ValueTask(s.WriteAsync("seed"u8.ToArray(), ct).AsTask()), TestContext.Current.CancellationToken);
            await Task.Delay(500, TestContext.Current.CancellationToken);
            factory.Entries.Clear();

            await File.WriteAllTextAsync(store.GetFullPath(id), "external", TestContext.Current.CancellationToken);
            await PumpAsync(scheduler, () => factory.Entries.Count > 0);

            var entry = Assert.Single(factory.Entries);
            Assert.Equal(3005, entry.EventId.Id);
            Assert.Equal(LogLevel.Debug, entry.Level);

            Directory.Delete(directory, recursive: true);
        }
    }
}
