using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using NetPrints.Core;
using NetPrints.Serialization;
using NetPrints.Serialization.Documents;
using NetPrints.Serialization.Json;
using NetPrints.Serialization.Legacy;
using NetPrints.Serialization.Mapping;
using NetPrints.Tests.Samples;
using Xunit;

namespace NetPrints.Tests.Serialization
{
    /// <summary>
    /// <see cref="LegacyXmlDocumentFormat"/>: both fixtures import without issues, and importing the
    /// same fixture twice gives byte-identical documents (DF-T18, legacy part).
    /// </summary>
    public class LegacyXmlDocumentFormatTests
    {
        private static readonly JsonSerializerOptions JsonOptions =
            new NetPrintsJsonOptions(new NodeDocumentConverterRegistry(NodeDocumentConverterRegistry.BuiltIn, [])).SerializerOptions;

        private static LegacyXmlDocumentFormat NewFormat() =>
            new(new DocumentMapper(new NodeDocumentConverterRegistry(NodeDocumentConverterRegistry.BuiltIn, [])));

        private static async Task<ClassDocument> ReadAsync(LegacyXmlDocumentFormat format, string path)
        {
            using FileStream stream = File.OpenRead(path);
            return await format.ReadClassAsync(
                stream, new DocumentId(Path.GetFileName(path) + ".json"), TestContext.Current.CancellationToken);
        }

        public static IEnumerable<object[]> FixturePaths()
        {
            string root = SampleProjectFactory.FindRepositoryRoot();
            yield return new object[]
            {
                Path.Combine(root, "tests", "NetPrints.Core.Tests", "Fixtures", "Legacy", "HelloWorld", "HelloWorld.Program.netpc"),
            };
            yield return new object[]
            {
                Path.Combine(root, "tests", "NetPrints.Core.Tests", "Fixtures", "Legacy", "AllNodes", "AllNodes.Everything.netpc"),
            };
        }

        [Theory]
        [MemberData(nameof(FixturePaths))]
        public async Task FixtureImportsWithoutIssues(string path)
        {
            var registry = new NodeDocumentConverterRegistry(NodeDocumentConverterRegistry.BuiltIn, []);
            var mapper = new DocumentMapper(registry);
            var format = new LegacyXmlDocumentFormat(mapper);

            ClassDocument document = await ReadAsync(format, path);

            var issues = new List<DocumentIssue>();
            mapper.FromDocument(document, Project.CreateNew("P", "P"), issues, new DocumentId(Path.GetFileName(path) + ".json"));

            Assert.Empty(issues);
        }

        [Theory]
        [MemberData(nameof(FixturePaths))]
        public async Task ImportingTheSameFixtureTwiceGivesByteIdenticalDocuments(string path)
        {
            LegacyXmlDocumentFormat format = NewFormat();

            ClassDocument first = await ReadAsync(format, path);
            ClassDocument second = await ReadAsync(format, path);

            JsonNode? firstNode = JsonSerializer.SerializeToNode(first, JsonOptions);
            JsonNode? secondNode = JsonSerializer.SerializeToNode(second, JsonOptions);
            Assert.True(JsonNode.DeepEquals(firstNode, secondNode));
        }
    }
}
