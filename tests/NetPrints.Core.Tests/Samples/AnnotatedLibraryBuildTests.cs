using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NetPrints.Catalog;
using NetPrints.Testing;
using NetPrints.Tests.Projects;
using Xunit;

namespace NetPrints.Tests.Samples
{
    /// <summary>
    /// AN-T12: a temporary NetPrints project references the annotated fixture library (<c>CatalogAnnotatedLib.dll</c>, which embeds its own
    /// catalog); its graph calls the <c>[NetPrintsNode]</c> method <c>Greeter.Greet</c>, and the project builds through a real
    /// <c>dotnet build</c> and prints the greeting. Nothing is packed or restored beyond the SDK layout the other sample tests use.
    /// </summary>
    public class AnnotatedLibraryBuildTests
    {
        [Fact]
        public void TheFixtureLibraryEmbedsTheCatalogTheGraphCallsInto()
        {
            CatalogDocument document = Assert.Single(EmbeddedCatalogReader.Read(FixtureExtensions.AnnotatedLibraryAssembly()));
            Assert.Contains(document.Types, type => type.Id == "T:AnnotatedFixture.Greeter");
        }

        [Fact]
        public async Task AGraphCallingAnAnnotatedNodeBuildsAndRunsInATemporaryProject()
        {
            string fixtureDir = Path.Combine(SampleProjectFactory.FindRepositoryRoot(), "tests", "NetPrints.Core.Tests", "Fixtures", "AnnotatedCall");
            string directory = Directory.CreateTempSubdirectory("netprints-an12-").FullName;
            try
            {
                File.Copy(Path.Combine(fixtureDir, "AnnotatedCall.Program.netpc.json"), Path.Combine(directory, "AnnotatedCall.Program.netpc.json"));
                await File.WriteAllTextAsync(Path.Combine(directory, "AnnotatedCall.csproj"), $"""
                    <Project Sdk="Microsoft.NET.Sdk">

                      <PropertyGroup>
                        <OutputType>Exe</OutputType>
                        <TargetFramework>net10.0</TargetFramework>
                        <RootNamespace>AnnotatedCall</RootNamespace>
                        <NetPrintsProfile>netprints.default</NetPrintsProfile>
                      </PropertyGroup>

                      <ItemGroup>
                        <Reference Include="CatalogAnnotatedLib">
                          <HintPath>{FixtureExtensions.AnnotatedLibraryAssembly()}</HintPath>
                        </Reference>
                        <PackageReference Include="NetPrints.Sdk" Version="1.0.0" Condition="'$(NetPrintsUseLocalSdk)' != 'true'" PrivateAssets="all" />
                      </ItemGroup>

                    </Project>

                    """, TestContext.Current.CancellationToken);

                LocalSdkLayout.Write(directory);
                (int buildExit, string buildOutput) = await ExternalProcess.RunDotnetAsync(directory, environment: null,
                    "build", Path.Combine(directory, "AnnotatedCall.csproj"), "-nodeReuse:false", "-v:n", "-tl:off", "--nologo");
                Assert.True(buildExit == 0, buildOutput);
                Assert.True(File.Exists(Path.Combine(directory, "AnnotatedCall.Program.netpc.g.cs")), buildOutput);

                (int runExit, string runOutput) = await ExternalProcess.RunDotnetAsync(directory, environment: null,
                    "run", "--project", Path.Combine(directory, "AnnotatedCall.csproj"), "--no-build");
                Assert.True(runExit == 0, runOutput);
                Assert.Equal("Hello, World!", runOutput.Trim());
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
