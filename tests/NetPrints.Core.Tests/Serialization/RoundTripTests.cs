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
using NetPrints.Serialization.Mapping;
using NetPrints.Serialization.Migrations;
using NetPrints.Tests.Characterization;
using NetPrints.Tests.Samples;
using NetPrints.Translator;
using Xunit;

namespace NetPrints.Tests.Serialization
{
    /// <summary>
    /// Graph-level golden-byte round trips (document-format.md §2.6, §7): DF-T02 (JSON fixture → load →
    /// mark dirty → save → load → C# equals golden), DF-T03 (load → mark dirty → save is byte-identical)
    /// and DF-T05 (moving one node changes exactly one <c>layout</c> line). Revised 2026-09-26
    /// (research.md R21): no legacy step; every source here is already-migrated JSON (T054a).
    /// </summary>
    public class RoundTripTests
    {
        private static NodeDocumentConverterRegistry NewRegistry() => new(NodeDocumentConverterRegistry.BuiltIn, []);

        private static JsonDocumentFormat NewJsonFormat(NodeDocumentConverterRegistry registry) =>
            new(new NetPrintsJsonOptions(registry), new DocumentMigrator([]));

        private static async Task<byte[]> ReadFileAsync(string path)
        {
            using var stream = new MemoryStream();
            await using (FileStream input = File.OpenRead(path))
            {
                await input.CopyToAsync(stream, TestContext.Current.CancellationToken);
            }

            return stream.ToArray();
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

        private static async Task<byte[]> SaveAsync(ClassGraph cls)
        {
            cls.MarkDirty();

            NodeDocumentConverterRegistry registry = NewRegistry();
            var mapper = new DocumentMapper(registry);
            JsonDocumentFormat jsonFormat = NewJsonFormat(registry);
            ClassDocument saved = mapper.ToDocument(cls);
            using var output = new MemoryStream();
            await jsonFormat.WriteClassAsync(saved, output, TestContext.Current.CancellationToken);
            return output.ToArray();
        }

        // DF-T02: JSON fixture -> load -> mark dirty -> save -> load -> C# equals the golden file
        // recorded before P1 (T004), the same golden GoldenCSharpTests (DF-T01) checks directly.
        [Theory]
        [MemberData(nameof(GoldenCSharpTests.Fixtures), MemberType = typeof(GoldenCSharpTests))]
        public async Task JsonFixtureThroughSaveAndReloadProducesGoldenCSharp(string fixtureName, string classFileName)
        {
            string root = SampleProjectFactory.FindRepositoryRoot();
            string goldenDir = Path.Combine(root, "tests", "NetPrints.Core.Tests", "Fixtures", "Golden");
            string fixturePath = Path.Combine(root, "tests", "NetPrints.Core.Tests", "Fixtures", fixtureName, classFileName);
            var id = new DocumentId(classFileName);

            byte[] original = await ReadFileAsync(fixturePath);
            ClassGraph cls = await LoadJsonAsync(original, id, fixtureName);
            byte[] saved = await SaveAsync(cls);
            ClassGraph reloaded = await LoadJsonAsync(saved, id, fixtureName);

            var translator = new ClassTranslator();
            string translated = translator.TranslateClass(reloaded);
            string golden = File.ReadAllText(Path.Combine(goldenDir, $"{reloaded.FullName}.cs"));
            Assert.Equal(golden, translated);
        }

        public static IEnumerable<object[]> RoundTripSources()
        {
            string root = SampleProjectFactory.FindRepositoryRoot();
            yield return new object[] { Path.Combine(root, "tests", "NetPrints.Core.Tests", "Fixtures", "HelloWorld", "HelloWorld.Program.netpc.json") };
            yield return new object[] { Path.Combine(root, "tests", "NetPrints.Core.Tests", "Fixtures", "AllNodes", "AllNodes.Everything.netpc.json") };
            yield return new object[] { Path.Combine(root, "samples", "HelloWorld", "HelloWorld.Program.netpc.json") };
        }

        // DF-T03: load -> mark dirty -> save reproduces the same canonical bytes, for every fixture and
        // the committed sample; the written bytes re-parse.
        [Theory]
        [MemberData(nameof(RoundTripSources))]
        public async Task JsonRoundTripIsByteIdentical(string jsonPath)
        {
            var id = new DocumentId(Path.GetFileName(jsonPath));
            byte[] canonical = await ReadFileAsync(jsonPath);
            ClassGraph cls = await LoadJsonAsync(canonical, id, "P");
            byte[] rewritten = await SaveAsync(cls);

            Assert.Equal(canonical, rewritten);
            Assert.NotNull(JsonNode.Parse(rewritten));
        }

        // DF-T05: moving one node changes exactly one line, inside `layout` (line diff of two saves).
        [Fact]
        public async Task MovingOneNodeChangesExactlyOneLayoutLine()
        {
            string root = SampleProjectFactory.FindRepositoryRoot();
            string jsonPath = Path.Combine(root, "tests", "NetPrints.Core.Tests", "Fixtures", "HelloWorld", "HelloWorld.Program.netpc.json");
            var id = new DocumentId("HelloWorld.Program.netpc.json");

            byte[] original = await ReadFileAsync(jsonPath);
            ClassGraph cls = await LoadJsonAsync(original, id, "HelloWorld");

            MethodGraph main = cls.Methods.Single();
            CallMethodNode moved = main.Nodes.OfType<CallMethodNode>().Single();
            moved.PositionX += 40;
            moved.PositionY += 20;

            byte[] updated = await SaveAsync(cls);

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
