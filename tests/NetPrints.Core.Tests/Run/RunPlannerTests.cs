using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NetPrints.Core;
using NetPrints.Projects;
using NetPrints.Run.Testing;
using NetPrints.Workspace;
using NetPrints.Workspace.Run;
using Xunit;

namespace NetPrints.Tests.Run;

/// <summary>run-profiles.md §7.3a: <see cref="RunPlanner"/> reads once, evaluates at most once and hands the result to the resolver.</summary>
public sealed class RunPlannerTests : IDisposable
{
    private const string TwoProfiles = """
        {
          "profiles": {
            "First": { "commandName": "Executable", "executablePath": "$(ToolDir)/tool", "commandLineArgs": "$(Shared)" },
            "Second": { "commandName": "Executable", "executablePath": "$(OtherDir)/tool", "commandLineArgs": "$(shared) $(Extra)" }
          }
        }
        """;

    private readonly string _root = Directory.CreateTempSubdirectory("np-plan-").FullName;
    private readonly string _project;

    public RunPlannerTests()
    {
        _project = Path.Combine(_root, "App.csproj");
        File.WriteAllText(_project, "<Project />");
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public async Task WithTheOutputTypeKnownOnlyTheChosenProfilesNamesAreEvaluatedOnce()
    {
        WriteSettings(TwoProfiles);
        var projects = new FakeRunProjectSystem(Properties(("ToolDir", "t"), ("Shared", "s")));

        RunResolution resolution = await Planner(projects).PlanAsync(
            new RunRequest(_project, RunConfigurations.Release) { OutputType = BinaryType.Executable },
            TestContext.Current.CancellationToken);

        (string configuration, IReadOnlyList<string> names) = Assert.Single(projects.Evaluations);
        Assert.Equal(RunConfigurations.Release, configuration);
        Assert.Equal(["ToolDir", "Shared"], names);
        Assert.Equal("First", resolution.Profile?.Name);
    }

    [Fact]
    public async Task WithTheOutputTypeKnownAProfileWithoutPropertiesEvaluatesNothing()
    {
        WriteSettings("""{"profiles":{"Plain":{"commandName":"Executable","executablePath":"x"}}}""");
        var projects = new FakeRunProjectSystem(Properties());

        await Planner(projects).PlanAsync(new RunRequest(_project, RunConfigurations.Debug) { OutputType = BinaryType.Executable }, TestContext.Current.CancellationToken);

        Assert.Empty(projects.Evaluations);
    }

    [Fact]
    public async Task WithTheOutputTypeKnownAndNoFileNothingIsEvaluated()
    {
        var projects = new FakeRunProjectSystem(Properties());

        RunResolution resolution = await Planner(projects).PlanAsync(new RunRequest(_project, RunConfigurations.Debug) { OutputType = BinaryType.Executable }, TestContext.Current.CancellationToken);

        Assert.Empty(projects.Evaluations);
        Assert.Null(resolution.Profile);
        Assert.NotNull(resolution.Request);
    }

    [Fact]
    public async Task ABlockedChoiceEvaluatesNothing()
    {
        WriteSettings(TwoProfiles);
        var projects = new FakeRunProjectSystem(Properties());

        RunResolution resolution = await Planner(projects).PlanAsync(
            new RunRequest(_project, RunConfigurations.Debug) { OutputType = BinaryType.Executable, ProfileName = "Nope" },
            TestContext.Current.CancellationToken);

        Assert.Empty(projects.Evaluations);
        Assert.Equal([NprDiagnosticCodes.UnknownProfile], resolution.Problems.Select(problem => problem.Code));
        Assert.True(resolution.IsBlocked);
    }

    [Fact]
    public async Task WithNoOutputTypeTheProjectIsEvaluatedOnceForTheOutputTypeAndEveryProfilesNames()
    {
        WriteSettings(TwoProfiles);
        var projects = new FakeRunProjectSystem(Properties(("OutputType", "Exe")));

        await Planner(projects).PlanAsync(new RunRequest(_project, RunConfigurations.Debug), TestContext.Current.CancellationToken);

        (string configuration, IReadOnlyList<string> names) = Assert.Single(projects.Evaluations);
        Assert.Equal(RunConfigurations.Debug, configuration);
        Assert.Equal(["OutputType", "ToolDir", "Shared", "OtherDir", "Extra"], names);
    }

    [Fact]
    public async Task WithNoOutputTypeAndNoFileOnlyTheOutputTypeIsEvaluated()
    {
        var projects = new FakeRunProjectSystem(Properties(("OutputType", "Exe")));

        RunResolution resolution = await Planner(projects).PlanAsync(new RunRequest(_project, RunConfigurations.Debug), TestContext.Current.CancellationToken);

        Assert.Equal(["OutputType"], Assert.Single(projects.Evaluations).Names);
        Assert.Contains("--no-launch-profile", Assert.IsType<ProcessStartRequest>(resolution.Request).Arguments);
    }

    [Theory]
    [InlineData("Exe", true)]
    [InlineData("exe", true)]
    [InlineData("WinExe", false)]
    [InlineData("Library", false)]
    [InlineData(null, false)]
    public async Task TheOutputTypeMapsExeToAnExecutableAndEverythingElseToALibrary(string? outputType, bool executable)
    {
        WriteSettings("""{"profiles":{"Run":{"commandName":"Project"}}}""");
        var projects = new FakeRunProjectSystem(outputType is null ? Properties() : Properties(("OutputType", outputType)));

        RunResolution resolution = await Planner(projects).PlanAsync(
            new RunRequest(_project, RunConfigurations.Debug) { ProfileName = "Run" },
            TestContext.Current.CancellationToken);

        Assert.Equal(!executable, resolution.IsBlocked);
        if (!executable)
        {
            Assert.Equal([NprDiagnosticCodes.ProjectKindNeedsExecutable], resolution.Problems.Select(problem => problem.Code));
        }
    }

    [Fact]
    public async Task TheConfigurationReachesTheProgramsArguments()
    {
        var projects = new FakeRunProjectSystem(Properties(("OutputType", "Exe")));

        RunResolution resolution = await Planner(projects).PlanAsync(new RunRequest(_project, RunConfigurations.Release), TestContext.Current.CancellationToken);

        string[] arguments = [.. Assert.IsType<ProcessStartRequest>(resolution.Request).Arguments];
        Assert.Equal("Release", arguments[Array.IndexOf(arguments, "-c") + 1]);
    }

    [Fact]
    public async Task TheEnvironmentIsReadAtEachPlanAndAnEmptyVariableRecordIsFine()
    {
        WriteSettings("""{"profiles":{"Run":{"commandName":"Project","commandLineArgs":"%STAMP%"}}}""");
        int reads = 0;
        var planner = new RunPlanner(
            new FakeRunProjectSystem(Properties(("OutputType", "Exe"))),
            RunPlatform.Linux,
            () => new Dictionary<string, string> { ["STAMP"] = $"read{++reads}" },
            () => []);

        RunResolution first = await planner.PlanAsync(new RunRequest(_project, RunConfigurations.Debug), TestContext.Current.CancellationToken);
        RunResolution second = await planner.PlanAsync(new RunRequest(_project, RunConfigurations.Debug), TestContext.Current.CancellationToken);

        Assert.Equal("read1", Assert.IsType<ProcessStartRequest>(first.Request).Arguments[^1]);
        Assert.Equal("read2", Assert.IsType<ProcessStartRequest>(second.Request).Arguments[^1]);
        Assert.Empty(Assert.IsType<ProcessStartRequest>(first.Request).EnvironmentRemovals);
    }

    [Fact]
    public void AProjectWithNoFolderHasNoLaunchSettings()
    {
        RunPlanner planner = Planner(new FakeRunProjectSystem(Properties()));

        Assert.Same(LaunchSettings.None, planner.ReadLaunchSettings("App.csproj"));
    }

    [Fact]
    public async Task AnUnreadableLaunchSettingsFileIsAProblemNotAnException()
    {
        string settings = WriteSettings("{}");
        File.Delete(settings);
        Directory.CreateDirectory(settings);
        var projects = new FakeRunProjectSystem(Properties(("OutputType", "Exe")));

        RunResolution resolution = await Planner(projects).PlanAsync(new RunRequest(_project, RunConfigurations.Debug), TestContext.Current.CancellationToken);

        Assert.True(resolution.IsBlocked);
        Assert.Equal(NprDiagnosticCodes.LaunchSettingsInvalid, Assert.Single(resolution.Problems).Code);
    }

    private static RunPlanner Planner(IRunProjectSystem projects) =>
        new(projects, RunPlatform.Linux, () => new Dictionary<string, string>(), () => []);

    private static Dictionary<string, string> Properties(params (string Name, string Value)[] entries) =>
        entries.ToDictionary(entry => entry.Name, entry => entry.Value, StringComparer.Ordinal);

    private string WriteSettings(string json)
    {
        string folder = Directory.CreateDirectory(Path.Combine(_root, "Properties")).FullName;
        string path = Path.Combine(folder, "launchSettings.json");
        File.WriteAllText(path, json);
        return path;
    }
}
