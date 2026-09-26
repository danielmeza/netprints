using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NetPrints.Generator;
using NetPrints.Serialization;
using NetPrints.Serialization.Json;
using NetPrints.Serialization.Mapping;
using NetPrints.Serialization.Migrations;
using NetPrints.Tests.Projects;
using Xunit;

namespace NetPrints.Tests.Samples
{
    /// <summary>
    /// DF-T01/SC-001 end to end, on top of the migrated fixtures (T054a, research.md R21): a temp copy
    /// of <c>samples/HelloWorld</c> actually builds and runs through a real <c>dotnet build</c>/<c>run</c>
    /// (<see cref="LocalSdkLayout"/> — <c>HelloWorldSampleTests</c> covers the in-process
    /// <see cref="NetPrints.Projects.IProjectSystem"/> path instead), and
    /// <see cref="GraphCodeGenerator.GenerateAsync"/> on the AllNodes fixture reproduces every golden
    /// <c>.g.cs</c> body byte for byte.
    /// </summary>
    public class MigratedFixtureBuildTests
    {
        [Fact]
        public async Task HelloWorldSampleBuildsAndRunsThroughARealDotnetBuild()
        {
            string directory = Directory.CreateTempSubdirectory("netprints-mfb-helloworld-").FullName;
            try
            {
                string source = Path.Combine(SampleProjectFactory.FindRepositoryRoot(), "samples", "HelloWorld");
                foreach (string file in Directory.GetFiles(source))
                {
                    File.Copy(file, Path.Combine(directory, Path.GetFileName(file)));
                }

                LocalSdkLayout.Write(directory);
                string csprojPath = Path.Combine(directory, "HelloWorld.csproj");

                (int buildExit, string buildOutput) = await ExternalProcess.RunDotnetAsync(directory, environment: null,
                    "build", csprojPath, "-v:n", "-tl:off", "--nologo");
                Assert.Equal(0, buildExit);
                Assert.True(File.Exists(Path.Combine(directory, "HelloWorld.Program.netpc.g.cs")), buildOutput);

                (int runExit, string runOutput) = await ExternalProcess.RunDotnetAsync(directory, environment: null,
                    "run", "--project", csprojPath, "--no-build");
                Assert.Equal(0, runExit);
                Assert.Contains("Hello, World!", runOutput);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        // DF-T01/SC-001: the generator's own API reproduces the golden C# (checked by GoldenCSharpTests
        // against the translator directly) inside its real header/write pipeline.
        [Fact]
        public async Task AllNodesGeneratesEveryGoldenBody()
        {
            string repositoryRoot = SampleProjectFactory.FindRepositoryRoot();
            string fixtureDir = Path.Combine(repositoryRoot, "tests", "NetPrints.Core.Tests", "Fixtures", "AllNodes");
            string goldenDir = Path.Combine(repositoryRoot, "tests", "NetPrints.Core.Tests", "Fixtures", "Golden");

            string directory = Directory.CreateTempSubdirectory("netprints-mfb-allnodes-").FullName;
            try
            {
                var registry = new NodeDocumentConverterRegistry(NodeDocumentConverterRegistry.BuiltIn, []);
                var mapper = new DocumentMapper(registry);
                var formats = new DocumentFormatRegistry([new JsonDocumentFormat(new NetPrintsJsonOptions(registry), new DocumentMigrator([]))]);
                var generator = new GraphCodeGenerator(formats, mapper);

                const string graphFileName = "AllNodes.Everything.netpc.json";
                string outputPath = Path.Combine(directory, "AllNodes.Everything.netpc.g.cs");
                var request = new GenerateRequest(Path.Combine(directory, "AllNodes.csproj"), "AllNodes", "netprints.default",
                    [new GraphJob(Path.Combine(fixtureDir, graphFileName), outputPath)], []);

                IReadOnlyList<GeneratedFileResult> results = await generator.GenerateAsync(request, TestContext.Current.CancellationToken);
                GeneratedFileResult result = Assert.Single(results);
                Assert.Empty(result.Diagnostics);
                Assert.True(result.Written);

                string golden = await File.ReadAllTextAsync(Path.Combine(goldenDir, "AllNodes.Everything.cs"), TestContext.Current.CancellationToken);
                string expected = GraphCodeGenerator.RenderFile(golden, graphFileName);
                string actual = await File.ReadAllTextAsync(outputPath, TestContext.Current.CancellationToken);

                Assert.Equal(expected, actual);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
