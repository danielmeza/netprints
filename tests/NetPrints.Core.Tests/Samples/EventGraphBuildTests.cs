using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NetPrints.Tests.Projects;
using Xunit;

namespace NetPrints.Tests.Samples
{
    /// <summary>
    /// T081 (US4): the G1 golden "EventGraphs.GameEvents" fixture (T079/T080) actually builds and runs
    /// through a real <c>dotnet build</c>/<c>run</c> (<see cref="LocalSdkLayout"/>, same pattern as
    /// <see cref="MigratedFixtureBuildTests"/>): a hand-written <c>Program.cs</c> calls
    /// <c>OnStart()</c>/<c>OnTick(float)</c> on an instance of the class the SDK's <c>NetPrintsGenerate</c>
    /// target generates from the checked-in <c>EventGraphs.GameEvents.netpc.json</c>, copied next to its
    /// hand-written base type <c>EventBase.cs</c> (both from <c>Fixtures/EventGraphs/</c>, which excludes
    /// them from this test project's own <c>Compile</c> items, document-format.md/T054a).
    /// </summary>
    public class EventGraphBuildTests
    {
        // The spawned program's own OnTick(1.5f) output is asserted against a literal "1.5": force the
        // invariant culture there (this test asserts the child process's number formatting, not
        // product code, which stays untouched) and MSBuild's own console messages to English.
        private static readonly IReadOnlyDictionary<string, string> BuildEnvironment =
            new Dictionary<string, string> { ["DOTNET_CLI_UI_LANGUAGE"] = "en" };

        private static readonly IReadOnlyDictionary<string, string> RunEnvironment =
            new Dictionary<string, string>
            {
                ["DOTNET_CLI_UI_LANGUAGE"] = "en",
                ["DOTNET_SYSTEM_GLOBALIZATION_INVARIANT"] = "1",
            };

        [Fact]
        public async Task GameEventsBuildsAndRunsOnStartAndOnTickThroughARealDotnetBuild()
        {
            string repositoryRoot = SampleProjectFactory.FindRepositoryRoot();
            string fixtureDir = Path.Combine(repositoryRoot, "tests", "NetPrints.Core.Tests", "Fixtures", "EventGraphs");

            string directory = Directory.CreateTempSubdirectory("netprints-mfb-eventgraphs-").FullName;
            try
            {
                File.Copy(Path.Combine(fixtureDir, "EventBase.cs"), Path.Combine(directory, "EventBase.cs"));
                File.Copy(Path.Combine(fixtureDir, "EventGraphs.GameEvents.netpc.json"), Path.Combine(directory, "EventGraphs.GameEvents.netpc.json"));

                // Not part of the graph: a hand-written entry point, the same role Fixtures/**/*.cs
                // plays for other fixtures (excluded from the test project's own Compile items, but a
                // real source file for a project built here).
                await File.WriteAllTextAsync(Path.Combine(directory, "Program.cs"), """
                    var events = new EventGraphs.GameEvents();
                    events.OnStart();
                    events.OnTick(1.5f);

                    """, TestContext.Current.CancellationToken);

                await File.WriteAllTextAsync(Path.Combine(directory, "EventGraphs.csproj"), """
                    <Project Sdk="Microsoft.NET.Sdk">

                      <PropertyGroup>
                        <OutputType>Exe</OutputType>
                        <TargetFramework>net10.0</TargetFramework>
                        <RootNamespace>EventGraphsFixture</RootNamespace>
                        <NetPrintsProfile>netprints.default</NetPrintsProfile>
                      </PropertyGroup>

                      <ItemGroup>
                        <PackageReference Include="NetPrints.Sdk" Version="1.0.0" Condition="'$(NetPrintsUseLocalSdk)' != 'true'" PrivateAssets="all" />
                      </ItemGroup>

                    </Project>

                    """, TestContext.Current.CancellationToken);

                LocalSdkLayout.Write(directory);
                string csprojPath = Path.Combine(directory, "EventGraphs.csproj");

                (int buildExit, string buildOutput) = await ExternalProcess.RunDotnetAsync(directory, BuildEnvironment,
                    "build", csprojPath, "-v:n", "-tl:off", "--nologo");
                Assert.True(buildExit == 0, buildOutput);
                Assert.True(File.Exists(Path.Combine(directory, "EventGraphs.GameEvents.netpc.g.cs")), buildOutput);

                (int runExit, string runOutput) = await ExternalProcess.RunDotnetAsync(directory, RunEnvironment,
                    "run", "--project", csprojPath, "--no-build");
                Assert.True(runExit == 0, runOutput);
                Assert.Contains("OnStart!", runOutput);
                Assert.Contains("1.5", runOutput);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
