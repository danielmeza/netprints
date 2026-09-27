using System.IO;
using System.Threading.Tasks;
using NetPrints.Tests.Projects;
using Xunit;

namespace NetPrints.Tests.Samples
{
    /// <summary>
    /// T085 (US5): the "Locals" golden fixture (an if/else-driven loop — <c>op_LessThan</c>/<c>op_Addition</c>,
    /// research.md's operator mechanism, not <c>ForLoopNode</c>, see implementation-notes.md "Sub-phase H,
    /// batch H1" — that increments a local variable <c>count</c> while it is below 5, then prints it)
    /// actually builds and runs through a real <c>dotnet build</c>/<c>run</c> (<see cref="LocalSdkLayout"/>,
    /// same pattern as <see cref="MigratedFixtureBuildTests"/> and <see cref="EventGraphBuildTests"/>):
    /// the class itself declares <c>Main</c>, so no separate hand-written entry point is needed (like
    /// <c>samples/HelloWorld</c>).
    /// </summary>
    public class LocalVariableBuildTests
    {
        [Fact]
        public async Task LocalsBuildsAndRunsALoopIncrementingALocalThroughARealDotnetBuild()
        {
            string repositoryRoot = SampleProjectFactory.FindRepositoryRoot();
            string fixtureDir = Path.Combine(repositoryRoot, "tests", "NetPrints.Core.Tests", "Fixtures", "Locals");

            string directory = Directory.CreateTempSubdirectory("netprints-mfb-locals-").FullName;
            try
            {
                File.Copy(Path.Combine(fixtureDir, "Locals.netpc.json"), Path.Combine(directory, "Locals.netpc.json"));

                await File.WriteAllTextAsync(Path.Combine(directory, "Locals.csproj"), """
                    <Project Sdk="Microsoft.NET.Sdk">

                      <PropertyGroup>
                        <OutputType>Exe</OutputType>
                        <TargetFramework>net10.0</TargetFramework>
                        <RootNamespace>LocalsFixture</RootNamespace>
                        <NetPrintsProfile>netprints.default</NetPrintsProfile>
                      </PropertyGroup>

                      <ItemGroup>
                        <PackageReference Include="NetPrints.Sdk" Version="1.0.0" Condition="'$(NetPrintsUseLocalSdk)' != 'true'" PrivateAssets="all" />
                      </ItemGroup>

                    </Project>

                    """, TestContext.Current.CancellationToken);

                LocalSdkLayout.Write(directory);
                string csprojPath = Path.Combine(directory, "Locals.csproj");

                (int buildExit, string buildOutput) = await ExternalProcess.RunDotnetAsync(directory, environment: null,
                    "build", csprojPath, "-v:n", "-tl:off", "--nologo");
                Assert.True(buildExit == 0, buildOutput);
                Assert.True(File.Exists(Path.Combine(directory, "Locals.netpc.g.cs")), buildOutput);

                (int runExit, string runOutput) = await ExternalProcess.RunDotnetAsync(directory, environment: null,
                    "run", "--project", csprojPath, "--no-build");
                Assert.True(runExit == 0, runOutput);
                Assert.Contains("5", runOutput);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
