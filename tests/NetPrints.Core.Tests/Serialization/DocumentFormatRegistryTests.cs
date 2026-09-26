using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NetPrints.Serialization;
using NetPrints.Serialization.Documents;
using Xunit;

namespace NetPrints.Tests.Serialization
{
    /// <summary>DF-T16: <see cref="DocumentFormatRegistry"/>.</summary>
    public class DocumentFormatRegistryTests
    {
        private sealed class FakeFormat : IDocumentFormat
        {
            public FakeFormat(string id, params string[] extensions)
            {
                Id = id;
                ClassExtensions = extensions;
            }

            public string Id { get; }

            public IReadOnlyList<string> ClassExtensions { get; }

            public bool CanWrite => true;

            public ValueTask<ClassDocument> ReadClassAsync(Stream input, DocumentId id, CancellationToken cancellationToken) =>
                throw new NotSupportedException();

            public ValueTask WriteClassAsync(ClassDocument document, Stream output, CancellationToken cancellationToken) =>
                throw new NotSupportedException();
        }

        private static FakeFormat JsonFormat() => new("json", ".netpc.json");
        private static FakeFormat LegacyFormat() => new("legacy-xml", ".netpc");

        [Fact]
        public void DuplicateIdThrows()
        {
            Assert.Throws<ArgumentException>(() =>
                new DocumentFormatRegistry([JsonFormat(), new FakeFormat("legacy-xml", ".a"), new FakeFormat("legacy-xml", ".b")]));
        }

        [Fact]
        public void DuplicateExtensionThrows()
        {
            Assert.Throws<ArgumentException>(() =>
                new DocumentFormatRegistry([JsonFormat(), new FakeFormat("legacy-xml", ".netpc.json")]));
        }

        [Fact]
        public void MissingJsonFormatThrows()
        {
            Assert.Throws<ArgumentException>(() => new DocumentFormatRegistry([LegacyFormat()]));
        }

        [Fact]
        public void DefaultIsTheJsonFormat()
        {
            FakeFormat json = JsonFormat();
            var registry = new DocumentFormatRegistry([json, LegacyFormat()]);

            Assert.Same(json, registry.Default);
        }

        [Fact]
        public void FindPrefersTheLongestMatchingExtension()
        {
            FakeFormat json = JsonFormat();
            FakeFormat legacy = LegacyFormat();
            var registry = new DocumentFormatRegistry([json, legacy]);

            Assert.Same(json, registry.Find(new DocumentId("C.netpc.json"), DocumentKind.Class));
            Assert.Same(legacy, registry.Find(new DocumentId("C.netpc"), DocumentKind.Class));
        }

        [Fact]
        public void FindReturnsNullForAnUnmatchedExtensionOrNonClassKind()
        {
            var registry = new DocumentFormatRegistry([JsonFormat(), LegacyFormat()]);

            Assert.Null(registry.Find(new DocumentId("C.txt"), DocumentKind.Class));
            Assert.Null(registry.Find(new DocumentId("C.netpc.json"), DocumentKind.Project));
        }
    }
}
