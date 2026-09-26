using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Serialization;
using NetPrints.Serialization.Documents;
using NetPrints.Serialization.Json;
using NetPrints.Serialization.Legacy;
using NetPrints.Serialization.Mapping;
using NetPrints.Serialization.Migrations;
using NetPrints.Tests.Characterization;
using NetPrints.Tests.Samples;
using NetPrints.Translator;
using Xunit;

namespace NetPrints.Tests.Serialization
{
    /// <summary>
    /// Graph-level golden-byte round trips (document-format.md §2.6): DF-T02 (legacy → JSON → load → C#),
    /// DF-T03 (load → mark dirty → save is byte-identical) and DF-T05 (moving one node changes exactly
    /// one <c>layout</c> line).
    /// </summary>
    public class RoundTripTests
    {
        private static NodeDocumentConverterRegistry NewRegistry() => new(NodeDocumentConverterRegistry.BuiltIn, []);

        private static JsonDocumentFormat NewJsonFormat(NodeDocumentConverterRegistry registry) =>
            new(new NetPrintsJsonOptions(registry), new DocumentMigrator([]));

        private static async Task<byte[]> ConvertLegacyToJsonAsync(string legacyPath, DocumentId id)
        {
            var mapper = new DocumentMapper(NewRegistry());
            var legacyFormat = new LegacyXmlDocumentFormat(mapper);

            ClassDocument document;
            using (FileStream input = File.OpenRead(legacyPath))
            {
                document = await legacyFormat.ReadClassAsync(input, id, TestContext.Current.CancellationToken);
            }

            JsonDocumentFormat jsonFormat = NewJsonFormat(NewRegistry());
            using var output = new MemoryStream();
            await jsonFormat.WriteClassAsync(document, output, TestContext.Current.CancellationToken);
            return output.ToArray();
        }

        private static async Task<ClassGraph> LoadJsonAsync(byte[] json, DocumentId id, string projectName)
        {
            NodeDocumentConverterRegistry registry = NewRegistry();
            var mapper = new DocumentMapper(registry);
            JsonDocumentFormat jsonFormat = NewJsonFormat(registry);

            ClassDocument document;
            using (var input = new MemoryStream(json))
            {
                document = await jsonFormat.ReadClassAsync(input, id, TestContext.Current.CancellationToken);
            }

            var issues = new List<DocumentIssue>();
            Project project = Project.CreateNew(projectName, projectName);
            ClassGraph cls = mapper.FromDocument(document, project, issues, id);
            Assert.Empty(issues);
            return cls;
        }

        // DF-T02: Legacy -> JSON -> load -> C# equals the golden file recorded before P1 (T004), the
        // same golden GoldenCSharpTests (DF-T01) checks the direct legacy import against.
        [Theory]
        [MemberData(nameof(GoldenCSharpTests.LegacyFixtures), MemberType = typeof(GoldenCSharpTests))]
        public async Task LegacyThroughJsonProducesGoldenCSharp(string fixtureName, string classFileName)
        {
            string root = SampleProjectFactory.FindRepositoryRoot();
            string legacyPath = Path.Combine(root, "tests", "NetPrints.Core.Tests", "Fixtures", "Legacy", fixtureName, classFileName);
            string goldenDir = Path.Combine(root, "tests", "NetPrints.Core.Tests", "Fixtures", "Golden");
            var id = new DocumentId(classFileName + ".json");

            byte[] json = await ConvertLegacyToJsonAsync(legacyPath, id);
            ClassGraph cls = await LoadJsonAsync(json, id, fixtureName);

            var translator = new ClassTranslator();
            string translated = translator.TranslateClass(cls);
            string golden = File.ReadAllText(Path.Combine(goldenDir, $"{cls.FullName}.cs"));
            Assert.Equal(golden, translated);
        }

        public static IEnumerable<object[]> RoundTripSources()
        {
            string root = SampleProjectFactory.FindRepositoryRoot();
            yield return new object[] { Path.Combine(root, "tests", "NetPrints.Core.Tests", "Fixtures", "Legacy", "HelloWorld", "HelloWorld.Program.netpc") };
            yield return new object[] { Path.Combine(root, "tests", "NetPrints.Core.Tests", "Fixtures", "Legacy", "AllNodes", "AllNodes.Everything.netpc") };
            yield return new object[] { Path.Combine(root, "samples", "HelloWorld", "HelloWorld.Program.netpc") };
        }

        // DF-T03: load -> mark dirty -> save reproduces the same canonical bytes, for every legacy
        // fixture and the (in-memory) converted sample; the written bytes re-parse.
        [Theory]
        [MemberData(nameof(RoundTripSources))]
        public async Task JsonRoundTripIsByteIdentical(string legacyPath)
        {
            var id = new DocumentId(Path.GetFileName(legacyPath) + ".json");
            byte[] canonical = await ConvertLegacyToJsonAsync(legacyPath, id);
            ClassGraph cls = await LoadJsonAsync(canonical, id, "P");
            cls.MarkDirty();

            NodeDocumentConverterRegistry registry = NewRegistry();
            var mapper = new DocumentMapper(registry);
            JsonDocumentFormat jsonFormat = NewJsonFormat(registry);
            ClassDocument saved = mapper.ToDocument(cls);
            using var output = new MemoryStream();
            await jsonFormat.WriteClassAsync(saved, output, TestContext.Current.CancellationToken);
            byte[] rewritten = output.ToArray();

            Assert.Equal(canonical, rewritten);
            Assert.NotNull(JsonNode.Parse(rewritten));
        }

        // DF-T05: moving one node changes exactly one line, inside `layout` (line diff of two saves).
        [Fact]
        public async Task MovingOneNodeChangesExactlyOneLayoutLine()
        {
            string root = SampleProjectFactory.FindRepositoryRoot();
            string legacyPath = Path.Combine(root, "tests", "NetPrints.Core.Tests", "Fixtures", "Legacy", "HelloWorld", "HelloWorld.Program.netpc");
            var id = new DocumentId("HelloWorld.Program.netpc.json");

            byte[] original = await ConvertLegacyToJsonAsync(legacyPath, id);
            ClassGraph cls = await LoadJsonAsync(original, id, "HelloWorld");

            MethodGraph main = cls.Methods.Single();
            CallMethodNode moved = main.Nodes.OfType<CallMethodNode>().Single();
            moved.PositionX += 40;
            moved.PositionY += 20;
            cls.MarkDirty();

            NodeDocumentConverterRegistry registry = NewRegistry();
            var mapper = new DocumentMapper(registry);
            JsonDocumentFormat jsonFormat = NewJsonFormat(registry);
            ClassDocument saved = mapper.ToDocument(cls);
            using var output = new MemoryStream();
            await jsonFormat.WriteClassAsync(saved, output, TestContext.Current.CancellationToken);
            byte[] updated = output.ToArray();

            string[] originalLines = Encoding.UTF8.GetString(original).Split('\n');
            string[] updatedLines = Encoding.UTF8.GetString(updated).Split('\n');
            Assert.Equal(originalLines.Length, updatedLines.Length);

            List<int> differingLines = Enumerable.Range(0, originalLines.Length)
                .Where(i => originalLines[i] != updatedLines[i])
                .ToList();
            int changedLine = Assert.Single(differingLines);

            int layoutHeaderLine = Array.FindIndex(originalLines, line => line.Contains("\"layout\":"));
            Assert.True(layoutHeaderLine >= 0 && changedLine > layoutHeaderLine, "The changed line must be inside 'layout'.");
            Assert.Matches(@"^\s*""[^""]+"":\s\[\d+,\s\d+\]$", originalLines[changedLine]);
            Assert.Matches(@"^\s*""[^""]+"":\s\[\d+,\s\d+\]$", updatedLines[changedLine]);
        }
    }
}
