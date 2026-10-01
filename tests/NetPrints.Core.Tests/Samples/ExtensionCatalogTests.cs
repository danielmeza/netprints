using System;
using System.IO;
using System.Threading.Tasks;
using NetPrints.Extensibility.Loading;
using NetPrints.Testing;
using NetPrints.Tests.Projects;
using Xunit;

namespace NetPrints.Tests.Samples
{
    /// <summary>
    /// CT-T15/SC-005 (extension path): a temporary project references <c>CatalogFixtureLib.dll</c> and the <c>fx.catalog</c>
    /// extension; its graph calls the cataloged <c>Fixture.Geometry.Vector2.Add</c> and the project builds through a
    /// real <c>dotnet build</c> and prints the expected result. The graph itself and the direct assembly reference do not
    /// depend on the extension, so this half proves the build loads the <c>NetPrintsExtension</c> folder (the second test
    /// fails a folder without an extension); <c>CatalogSearchTests</c> covers the catalog half of CT-T15.
    /// </summary>
    public class ExtensionCatalogTests
    {
        [Fact]
        public async Task AGraphCallingACatalogedMethodBuildsAndRunsInATemporaryProject()
        {
            (int buildExit, string buildOutput, string directory) = await BuildAsync(FixtureExtensions.CatalogFolder());
            try
            {
                Assert.True(buildExit == 0, buildOutput);
                Assert.DoesNotContain(ExtensionDiagnosticCodes.InvalidManifest, buildOutput, StringComparison.Ordinal);
                Assert.True(File.Exists(Path.Combine(directory, "CatalogCall.Program.netpc.g.cs")), buildOutput);

                (int runExit, string runOutput) = await ExternalProcess.RunDotnetAsync(directory, environment: null,
                    "run", "--project", Path.Combine(directory, "CatalogCall.csproj"), "--no-build");
                Assert.True(runExit == 0, runOutput);
                Assert.Equal("10", runOutput.Trim());
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [Fact]
        public async Task TheBuildLoadsTheExtensionFolderItIsGivenSoAFolderWithoutOneFailsIt()
        {
            string emptyFolder = Directory.CreateTempSubdirectory("netprints-ct15-empty-").FullName;
            (int buildExit, string buildOutput, string directory) = await BuildAsync(emptyFolder);
            try
            {
                Assert.NotEqual(0, buildExit);
                Assert.Contains(ExtensionDiagnosticCodes.InvalidManifest, buildOutput, StringComparison.Ordinal);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
                Directory.Delete(emptyFolder, recursive: true);
            }
        }

        private static async Task<(int ExitCode, string Output, string Directory)> BuildAsync(string extensionFolder)
        {
            string fixtureDir = Path.Combine(SampleProjectFactory.FindRepositoryRoot(), "tests", "NetPrints.Core.Tests", "Fixtures", "CatalogCall");
            string directory = Directory.CreateTempSubdirectory("netprints-ct15-").FullName;
            try
            {
                File.Copy(Path.Combine(fixtureDir, "CatalogCall.Program.netpc.json"), Path.Combine(directory, "CatalogCall.Program.netpc.json"));
                await File.WriteAllTextAsync(Path.Combine(directory, "CatalogCall.csproj"), $"""
                    <Project Sdk="Microsoft.NET.Sdk">

                      <PropertyGroup>
                        <OutputType>Exe</OutputType>
                        <TargetFramework>net10.0</TargetFramework>
                        <RootNamespace>CatalogCall</RootNamespace>
                        <NetPrintsProfile>netprints.default</NetPrintsProfile>
                      </PropertyGroup>

                      <ItemGroup>
                        <Reference Include="CatalogFixtureLib">
                          <HintPath>{FixtureExtensions.CatalogLibraryAssembly()}</HintPath>
                        </Reference>
                        <NetPrintsExtension Include="{extensionFolder}" />
                        <PackageReference Include="NetPrints.Sdk" Version="1.0.0" Condition="'$(NetPrintsUseLocalSdk)' != 'true'" PrivateAssets="all" />
                      </ItemGroup>

                    </Project>

                    """, TestContext.Current.CancellationToken);

                LocalSdkLayout.Write(directory);
                (int exitCode, string output) = await ExternalProcess.RunDotnetAsync(directory, environment: null,
                    "build", Path.Combine(directory, "CatalogCall.csproj"), "-v:n", "-tl:off", "--nologo");
                return (exitCode, output, directory);
            }
            catch
            {
                Directory.Delete(directory, recursive: true);
                throw;
            }
        }
    }
}
