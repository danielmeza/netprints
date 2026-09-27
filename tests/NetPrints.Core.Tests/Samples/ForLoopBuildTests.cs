using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NetPrints.Tests.Projects;
using Xunit;

namespace NetPrints.Tests.Samples
{
    /// <summary>
    /// Bug-fix batch (implementation-notes.md "ForLoopNode cannot loop"): the "ForLoop" fixture (a
    /// <c>ForLoopNode</c> from 0 to 3 whose body prints the loop index, followed by a second, empty-body
    /// <c>ForLoopNode</c> and a final print) actually builds and runs through a real <c>dotnet build</c>/
    /// <c>run</c> (<see cref="LocalSdkLayout"/>, same pattern as <see cref="LocalVariableBuildTests"/> and
    /// <see cref="EventGraphBuildTests"/>). Before the fix, <c>TranslateContinueForLoopNode</c> exited the
    /// loop after a single pass, so the first loop printed only "0"; after the fix it prints "0", "1", "2"
    /// and the second, empty-body loop still hands off correctly to the final "done" print.
    /// </summary>
    public class ForLoopBuildTests
    {
        [Fact]
        public async Task ForLoopBuildsAndRunsAThreeIterationLoopThroughARealDotnetBuild()
        {
            string repositoryRoot = SampleProjectFactory.FindRepositoryRoot();
            string fixtureDir = Path.Combine(repositoryRoot, "tests", "NetPrints.Core.Tests", "Fixtures", "ForLoop");

            string directory = Directory.CreateTempSubdirectory("netprints-mfb-forloop-").FullName;
            try
            {
                File.Copy(Path.Combine(fixtureDir, "ForLoop.netpc.json"), Path.Combine(directory, "ForLoop.netpc.json"));

                await File.WriteAllTextAsync(Path.Combine(directory, "ForLoop.csproj"), """
                    <Project Sdk="Microsoft.NET.Sdk">

                      <PropertyGroup>
                        <OutputType>Exe</OutputType>
                        <TargetFramework>net10.0</TargetFramework>
                        <RootNamespace>ForLoopFixture</RootNamespace>
                        <NetPrintsProfile>netprints.default</NetPrintsProfile>
                      </PropertyGroup>

                      <ItemGroup>
                        <PackageReference Include="NetPrints.Sdk" Version="1.0.0" Condition="'$(NetPrintsUseLocalSdk)' != 'true'" PrivateAssets="all" />
                      </ItemGroup>

                    </Project>

                    """, TestContext.Current.CancellationToken);

                LocalSdkLayout.Write(directory);
                string csprojPath = Path.Combine(directory, "ForLoop.csproj");

                (int buildExit, string buildOutput) = await ExternalProcess.RunDotnetAsync(directory, environment: null,
                    "build", csprojPath, "-v:n", "-tl:off", "--nologo");
                Assert.True(buildExit == 0, buildOutput);
                Assert.True(File.Exists(Path.Combine(directory, "ForLoop.netpc.g.cs")), buildOutput);

                (int runExit, string runOutput) = await ExternalProcess.RunDotnetAsync(directory, environment: null,
                    "run", "--project", csprojPath, "--no-build");
                Assert.True(runExit == 0, runOutput);

                string[] lines = runOutput
                    .Split('\n')
                    .Select(line => line.Trim('\r', '\n', ' '))
                    .Where(line => line.Length > 0)
                    .ToArray();

                Assert.Equal(["0", "1", "2", "done"], lines);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
