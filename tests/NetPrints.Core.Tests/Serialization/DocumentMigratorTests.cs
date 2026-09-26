using System;
using System.Text.Json.Nodes;
using NetPrints.Serialization;
using NetPrints.Serialization.Migrations;
using Xunit;

namespace NetPrints.Tests.Serialization
{
    /// <summary>DF-T10: <see cref="DocumentMigrator"/>.</summary>
    public class DocumentMigratorTests
    {
        private sealed class RenamePropertyMigration : IDocumentMigration
        {
            public DocumentKind Kind => DocumentKind.Class;
            public int FromVersion => 1;

            public void Migrate(JsonObject document)
            {
                document["renamed"] = document["old"]?.DeepClone();
                document.Remove("old");
                document["schemaVersion"] = 2;
            }
        }

        [Fact]
        public void NoMigrationsSupportsOnlyCurrentSchemaVersion()
        {
            var migrator = new DocumentMigrator([]);
            Assert.Equal(DocumentMigrator.CurrentSchemaVersion, migrator.Supported);
        }

        [Fact]
        public void MissingSchemaVersionThrows()
        {
            var migrator = new DocumentMigrator([]);
            var document = new JsonObject();

            Assert.Throws<DocumentFormatException>(() => migrator.Upgrade(document, DocumentKind.Class, new DocumentId("a.netpc.json")));
        }

        [Fact]
        public void NewerThanSupportedThrowsVersionException()
        {
            var migrator = new DocumentMigrator([]);
            var document = new JsonObject { ["schemaVersion"] = 2 };

            var ex = Assert.Throws<DocumentVersionException>(() =>
                migrator.Upgrade(document, DocumentKind.Class, new DocumentId("a.netpc.json")));
            Assert.Equal(2, ex.Found);
            Assert.Equal(1, ex.Supported);
        }

        [Fact]
        public void ZeroOrNegativeSchemaVersionThrows()
        {
            var migrator = new DocumentMigrator([]);
            var document = new JsonObject { ["schemaVersion"] = 0 };

            Assert.Throws<DocumentFormatException>(() => migrator.Upgrade(document, DocumentKind.Class, new DocumentId("a.netpc.json")));
        }

        [Fact]
        public void SyntheticMigrationUpgradesTheDocument()
        {
            var migrator = new DocumentMigrator([new RenamePropertyMigration()]);
            Assert.Equal(2, migrator.Supported);

            var document = new JsonObject { ["schemaVersion"] = 1, ["old"] = "value" };
            JsonObject upgraded = migrator.Upgrade(document, DocumentKind.Class, new DocumentId("a.netpc.json"));

            Assert.Same(document, upgraded);
            Assert.Equal(2, (int)upgraded["schemaVersion"]!);
            Assert.Equal("value", (string?)upgraded["renamed"]);
            Assert.False(upgraded.ContainsKey("old"));
        }

        [Fact]
        public void DuplicateMigrationForSameKindAndVersionThrows()
        {
            var migrations = new IDocumentMigration[] { new RenamePropertyMigration(), new RenamePropertyMigration() };
            Assert.Throws<ArgumentException>(() => new DocumentMigrator(migrations));
        }

        private sealed class GapMigration : IDocumentMigration
        {
            public DocumentKind Kind => DocumentKind.Class;
            public int FromVersion => 2;
            public void Migrate(JsonObject document) => document["schemaVersion"] = 3;
        }

        [Fact]
        public void GapInMigrationChainThrows()
        {
            Assert.Throws<ArgumentException>(() => new DocumentMigrator([new GapMigration()]));
        }
    }
}
