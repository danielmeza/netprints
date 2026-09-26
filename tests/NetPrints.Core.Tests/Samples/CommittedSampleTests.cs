using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NetPrints.Core;
using NetPrints.Generator;
using NetPrints.Serialization;
using NetPrints.Serialization.Documents;
using NetPrints.Serialization.Json;
using NetPrints.Serialization.Mapping;
using NetPrints.Serialization.Migrations;
using NetPrints.Tests.Characterization;
using Xunit;

namespace NetPrints.Tests.Samples
{
    /// <summary>
    /// DF-T26: every committed <c>samples/**/*.netpc.json</c> is canonical (load, mark dirty, save
    /// equals the file on disk) and every <c>samples/**/*.netpc.g.cs</c> is up to date with
    /// <see cref="GraphCodeGenerator.RenderFile"/> of its graph — a stale-file guard that runs in CI
    /// with the rest of the suite, so a forgotten regeneration fails here instead of only showing up as
    /// a diff on someone else's next build.
    /// </summary>
    public class CommittedSampleTests
    {
        private static NodeDocumentConverterRegistry NewRegistry() => new(NodeDocumentConverterRegistry.BuiltIn, []);

        private static JsonDocumentFormat NewJsonFormat(NodeDocumentConverterRegistry registry) =>
            new(new NetPrintsJsonOptions(registry), new DocumentMigrator([]));

        private static string SamplesDirectory() => Path.Combine(SampleProjectFactory.FindRepositoryRoot(), "samples");

        public static IEnumerable<object[]> GraphFiles() =>
            Directory.EnumerateFiles(SamplesDirectory(), "*.netpc.json", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => new object[] { path });

        [Theory]
        [MemberData(nameof(GraphFiles))]
        public async Task GraphIsCanonical(string graphPath)
        {
            var ct = TestContext.Current.CancellationToken;
            NodeDocumentConverterRegistry registry = NewRegistry();
            var mapper = new DocumentMapper(registry);
            JsonDocumentFormat format = NewJsonFormat(registry);
            var id = new DocumentId(Path.GetFileName(graphPath));

            byte[] original = await File.ReadAllBytesAsync(graphPath, ct);

            ClassDocument document;
            using (var input = new MemoryStream(original))
            {
                document = await format.ReadClassAsync(input, id, ct);
            }

            var issues = new List<DocumentIssue>();
            Project project = Project.CreateNew("Sample", "Sample", addDefaultReferences: false);
            ClassGraph cls = mapper.FromDocument(document, project, issues, id);
            Assert.Empty(issues);
            cls.MarkDirty();

            ClassDocument rewritten = mapper.ToDocument(cls);
            using var output = new MemoryStream();
            await format.WriteClassAsync(rewritten, output, ct);
            byte[] canonical = output.ToArray();

            bool update = Environment.GetEnvironmentVariable(GoldenCSharpTests.UpdateSnapshotsVariable) == "1";
            if (update)
            {
                await File.WriteAllBytesAsync(graphPath, canonical, ct);
            }

            byte[] onDisk = update ? canonical : original;
            Assert.True(onDisk.AsSpan().SequenceEqual(canonical),
                $"{graphPath} is not canonical; load it, mark it dirty and save it (or set " +
                $"{GoldenCSharpTests.UpdateSnapshotsVariable}=1 to rewrite it here) to put it in canonical form.");
        }

        public static IEnumerable<object[]> GeneratedFiles() =>
            Directory.EnumerateFiles(SamplesDirectory(), "*.netpc.g.cs", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => new object[] { path });

        [Theory]
        [MemberData(nameof(GeneratedFiles))]
        public async Task GeneratedFileIsUpToDate(string generatedPath)
        {
            var ct = TestContext.Current.CancellationToken;
            string graphPath = string.Concat(generatedPath.AsSpan(0, generatedPath.Length - ".g.cs".Length), ".json");
            Assert.True(File.Exists(graphPath), $"{generatedPath} has no matching graph file at {graphPath}.");

            NodeDocumentConverterRegistry registry = NewRegistry();
            var mapper = new DocumentMapper(registry);
            var formats = new DocumentFormatRegistry([NewJsonFormat(registry)]);
            var generator = new GraphCodeGenerator(formats, mapper);

            string directory = Directory.CreateTempSubdirectory("netprints-committed-sample-").FullName;
            try
            {
                string outputPath = Path.Combine(directory, Path.GetFileName(generatedPath));
                var request = new GenerateRequest(graphPath, null, "netprints.default", [new GraphJob(graphPath, outputPath)], []);
                IReadOnlyList<GeneratedFileResult> results = await generator.GenerateAsync(request, ct);
                GeneratedFileResult result = Assert.Single(results);
                Assert.Empty(result.Diagnostics);

                string expected = await File.ReadAllTextAsync(outputPath, ct);

                bool update = Environment.GetEnvironmentVariable(GoldenCSharpTests.UpdateSnapshotsVariable) == "1";
                if (update)
                {
                    File.Copy(outputPath, generatedPath, overwrite: true);
                }

                string actual = await File.ReadAllTextAsync(generatedPath, ct);
                Assert.True(expected == actual,
                    $"{generatedPath} is stale; run the generator (dotnet build the sample) or set " +
                    $"{GoldenCSharpTests.UpdateSnapshotsVariable}=1 to refresh it.");
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
