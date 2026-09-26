using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NetPrints.Core;
using NetPrints.Serialization;
using NetPrints.Serialization.Documents;
using NetPrints.Serialization.Json;
using NetPrints.Serialization.Mapping;
using NetPrints.Serialization.Migrations;
using NetPrints.Tests.Samples;
using Xunit;

namespace NetPrints.Tests.Projects
{
    /// <summary>
    /// PS-T05 (project-system.md §7): package mode. Packs <c>NetPrints.Sdk</c> into a temporary local
    /// feed and builds a project that consumes it purely as a <c>PackageReference</c> — no in-repo
    /// development mode (<see cref="LocalSdkLayout"/> is not used here) and an isolated NuGet global
    /// packages folder — proving the published package's own layout (project-system.md §2) works end
    /// to end, independently of <see cref="SdkTargetsTests"/>'s in-repo dev mode coverage.
    /// </summary>
    public class SdkPackageTests
    {
        private const string TestVersion = "0.0.1-ps-t05";

        private static readonly NodeDocumentConverterRegistry Registry = new(NodeDocumentConverterRegistry.BuiltIn, []);

        private static Task<(int ExitCode, string Output)> RunDotnetAsync(string workingDirectory,
            IReadOnlyDictionary<string, string>? environment, params string[] args) =>
            ExternalProcess.RunDotnetAsync(workingDirectory, environment, args);

        private static async Task WriteEmptyClassGraphAsync(string path, string ns, string name)
        {
            Project project = Project.CreateNew(ns, ns, addDefaultReferences: false);
            var cls = new ClassGraph { Name = name, Namespace = ns, Visibility = MemberVisibility.Public, Project = project };
            var mapper = new DocumentMapper(Registry);
            ClassDocument document = mapper.ToDocument(cls);
            var format = new JsonDocumentFormat(new NetPrintsJsonOptions(Registry), new DocumentMigrator([]));
            await using FileStream output = File.Create(path);
            await format.WriteClassAsync(document, output, TestContext.Current.CancellationToken);
        }

        [Fact]
        public async Task PackagedSdkBuildsAndGeneratesThroughAPlainPackageReference()
        {
            string repositoryRoot = SampleProjectFactory.FindRepositoryRoot();
            string sdkProjectPath = Path.Combine(repositoryRoot, "src", "NetPrints.Sdk", "NetPrints.Sdk.csproj");

            string feedDir = Directory.CreateTempSubdirectory("netprints-sdk-feed-").FullName;
            string packagesDir = Directory.CreateTempSubdirectory("netprints-sdk-packages-").FullName;
            string projectDir = Directory.CreateTempSubdirectory("netprints-sdk-package-project-").FullName;
            try
            {
                (int packExit, string packOutput) = await RunDotnetAsync(repositoryRoot, environment: null,
                    "pack", sdkProjectPath, "-c", "Release", "-o", feedDir, $"-p:Version={TestVersion}", "--nologo");
                Assert.True(packExit == 0, packOutput);

                // <clear/> drops every inherited source (this machine's, and any user-wide config), so
                // restore can only ever resolve NetPrints.Sdk from the local feed just packed above.
                File.WriteAllText(Path.Combine(projectDir, "nuget.config"), $"""
                    <?xml version="1.0" encoding="utf-8"?>
                    <configuration>
                      <packageSources>
                        <clear />
                        <add key="local" value="{feedDir}" />
                      </packageSources>
                    </configuration>

                    """);

                string graphPath = Path.Combine(projectDir, "PsT05.A.netpc.json");
                await WriteEmptyClassGraphAsync(graphPath, "PsT05", "A");

                string csprojPath = Path.Combine(projectDir, "PsT05.csproj");
                File.WriteAllText(csprojPath, $"""
                    <Project Sdk="Microsoft.NET.Sdk">
                      <PropertyGroup>
                        <TargetFramework>net10.0</TargetFramework>
                        <RootNamespace>PsT05</RootNamespace>
                        <NetPrintsProfile>netprints.default</NetPrintsProfile>
                      </PropertyGroup>
                      <ItemGroup>
                        <PackageReference Include="NetPrints.Sdk" Version="{TestVersion}" PrivateAssets="all" />
                      </ItemGroup>
                    </Project>

                    """);

                // Isolated global packages folder: a stale copy of NetPrints.Sdk <see cref="TestVersion"/>
                // in the machine's real one (from a previous run, or a developer's own manual pack)
                // must never be what this test actually resolves against.
                var environment = new Dictionary<string, string> { ["NUGET_PACKAGES"] = packagesDir };
                (int buildExit, string buildOutput) = await RunDotnetAsync(projectDir, environment,
                    "build", csprojPath, "-v:n", "-tl:off", "--nologo");

                Assert.True(buildExit == 0, buildOutput);
                Assert.True(File.Exists(Path.Combine(projectDir, "PsT05.A.netpc.g.cs")), buildOutput);
            }
            finally
            {
                Directory.Delete(feedDir, recursive: true);
                Directory.Delete(packagesDir, recursive: true);
                Directory.Delete(projectDir, recursive: true);
            }
        }
    }
}
