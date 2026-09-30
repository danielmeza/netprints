using System;
using System.IO;
using System.Threading.Tasks;
using NetPrints.Testing;
using NetPrints.Tests.Projects;
using Xunit;

namespace NetPrints.Tests.Samples
{
    /// <summary>
    /// CT-T15/SC-005 (extension path): a temporary project references <c>CatalogFixtureLib.dll</c> and the <c>fx.catalog</c>
    /// extension; its graph calls the cataloged <c>Fixture.Geometry.Vector2.Add</c> and the project builds through a
    /// real <c>dotnet build</c> and prints the expected result.
    /// </summary>
    public class ExtensionCatalogTests
    {
        [Fact]
        public async Task AGraphCallingACatalogedMethodBuildsAndRunsInATemporaryProject()
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
                        <NetPrintsExtension Include="{FixtureExtensions.CatalogFolder()}" />
                        <PackageReference Include="NetPrints.Sdk" Version="1.0.0" Condition="'$(NetPrintsUseLocalSdk)' != 'true'" PrivateAssets="all" />
                      </ItemGroup>

                    </Project>

                    """, TestContext.Current.CancellationToken);

                LocalSdkLayout.Write(directory);
                string csprojPath = Path.Combine(directory, "CatalogCall.csproj");

                (int buildExit, string buildOutput) = await ExternalProcess.RunDotnetAsync(directory, environment: null,
                    "build", csprojPath, "-v:n", "-tl:off", "--nologo");
                Assert.True(buildExit == 0, buildOutput);
                Assert.True(File.Exists(Path.Combine(directory, "CatalogCall.Program.netpc.g.cs")), buildOutput);

                (int runExit, string runOutput) = await ExternalProcess.RunDotnetAsync(directory, environment: null,
                    "run", "--project", csprojPath, "--no-build");
                Assert.True(runExit == 0, runOutput);
                Assert.Equal("10", runOutput.Trim());
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
