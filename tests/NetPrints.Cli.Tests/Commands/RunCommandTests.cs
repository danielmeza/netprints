using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using NetPrints.Cli.Commands;
using NetPrints.Cli.Tests.Support;
using NetPrints.Projects;
using NetPrints.Run.Testing;
using NetPrints.Workspace.Run;
using Xunit;

namespace NetPrints.Cli.Tests.Commands;

/// <summary>CL-T05: <c>run</c> builds, then runs the program with the arguments after <c>--</c> and returns its exit code.</summary>
public sealed class RunCommandTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("np-run-").FullName;
    private readonly string _project;
    private readonly CliTestHost _host;

    public RunCommandTests()
    {
        _project = Path.Combine(_root, "Program.csproj");
        File.WriteAllText(_project, "<Project />");
        _host = new CliTestHost(_root);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public async Task SuccessfulRunPropagatesTheChildsNonZeroExitCode()
    {
        _host.Processes.Result = new ProcessResult(5, "", "boom");

        int exitCode = await _host.RunAsync("run");

        Assert.Equal(5, exitCode);
        Assert.Contains("boom", _host.Error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SuccessfulRunWithZeroChildExitCodeReturnsExitCode0AndForwardsItsOutput()
    {
        _host.Processes.Result = new ProcessResult(0, "Hello, World!", "");

        int exitCode = await _host.RunAsync("run");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("Hello, World!", _host.Output, StringComparison.Ordinal);
        Assert.Equal([_project], _host.RunProjects.Builds.Select(build => build.ProjectFilePath));
    }

    [Fact]
    public async Task ArgumentsAfterTheSeparatorReachTheProgram()
    {
        int exitCode = await _host.RunAsync("run", "--", "one", "--two", "-r");

        Assert.Equal(ExitCodes.Success, exitCode);
        ProcessStartRequest started = Assert.Single(_host.Processes.Started);
        Assert.Equal([.. NoProfileArguments, "--", "one", "--two", "-r"], started.Arguments);
    }

    private string[] NoProfileArguments => ["run", "--project", _project, "--no-build", "--no-launch-profile", "-c", "Debug"];

    public static TheoryData<string[]> VerbatimTails => new()
    {
        { [""] },
        { ["-"] },
        { ["--="] },
        { ["a b", "--name", ""] },
        { ["one", "--", "two", "-p"] },
        { ["--"] },
        { ["--verbose", "-h", "--help"] },
    };

    [Theory]
    [MemberData(nameof(VerbatimTails))]
    public async Task TheTailAfterTheFirstSeparatorIsForwardedVerbatim(string[] tail)
    {
        int exitCode = await _host.RunAsync(["run", _project, "--", .. tail]);

        Assert.Equal(ExitCodes.Success, exitCode);
        ProcessStartRequest started = Assert.Single(_host.Processes.Started);
        Assert.Equal([.. NoProfileArguments, "--", .. tail], started.Arguments);
    }

    [Fact]
    public async Task ATailOptionNamedLikeAToolOptionIsNotConsumedByTheTool()
    {
        int exitCode = await _host.RunAsync("run", "--", "--verbose");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal("--verbose", Assert.Single(_host.Processes.Started).Arguments[^1]);
        Assert.DoesNotContain("info:", _host.Error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARealProgramReceivesAnEmptyArgumentAndALoneDash()
    {
        var sample = new SampleCopy();
        try
        {
            var host = new CliTestHost(sample.Directory);

            int exitCode = await host.RunRealAsync("run", sample.Project, "--", "", "--=", "-");

            Assert.Equal(ExitCodes.Success, exitCode);
            Assert.Contains("Hello, World!", host.Output, StringComparison.Ordinal);
        }
        finally
        {
            sample.Dispose();
        }
    }

    [Fact]
    public async Task TheProgramsOutputIsWrittenByTheRunnerNotBufferedByTheCommand()
    {
        _host.Processes.Result = new ProcessResult(0, "out\n", "err\n");

        Assert.Equal(ExitCodes.Success, await _host.RunAsync("run"));

        Assert.Equal("out\n", _host.Output.Replace("\r", "", StringComparison.Ordinal).Replace("Build succeeded.\n", "", StringComparison.Ordinal));
        Assert.Equal("err\n", _host.Error.ToString().Replace("\r", "", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TheProjectArgumentSelectsTheProjectAndArgumentsStillFollow()
    {
        string subdirectory = Directory.CreateDirectory(Path.Combine(_root, "Sub")).FullName;
        string other = Path.Combine(subdirectory, "Other.csproj");
        await File.WriteAllTextAsync(other, "<Project />", TestContext.Current.CancellationToken);

        Assert.Equal(ExitCodes.Success, await _host.RunAsync("run", other, "--", "x"));

        Assert.Equal([other], _host.RunProjects.Builds.Select(build => build.ProjectFilePath));
        Assert.Equal("x", Assert.Single(_host.Processes.Started).Arguments[^1]);
    }

    [Fact]
    public async Task NoArgumentsAddsNoSeparator()
    {
        await _host.RunAsync("run");

        Assert.DoesNotContain("--", Assert.Single(_host.Processes.Started).Arguments);
    }

    [Fact]
    public async Task BuildFailureReturnsExitCode1WithoutRunning()
    {
        _host.RunProjects.BuildResult = new BuildResult(false, [new ProjectMessage(ProjectMessageSeverity.Error, "CS1002", "; expected", "Program.cs", 3, 7)], null, "");

        int exitCode = await _host.RunAsync("run");

        Assert.Equal(ExitCodes.Failed, exitCode);
        Assert.Empty(_host.Processes.Started);
        Assert.Contains("Build failed with 1 error(s).", _host.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NoCompatibleSdkReturnsExitCode3()
    {
        _host.MsBuild.Available = false;

        Assert.Equal(ExitCodes.NoSdk, await _host.RunAsync("run"));
        Assert.Empty(_host.Processes.Started);
    }

    private static string Quote(string value) => $"\"{JsonEncodedText.Encode(value)}\"";

    private string ToolPath => Path.Combine(_root, "tool");

    private string ToolProfile(string name, string? args = null, string? workingDirectory = null, string? environment = null) =>
        $"{Quote(name)}: {{ \"commandName\": \"Executable\", \"executablePath\": {Quote(ToolPath)}"
        + (args is null ? string.Empty : $", \"commandLineArgs\": {Quote(args)}")
        + (workingDirectory is null ? string.Empty : $", \"workingDirectory\": {Quote(workingDirectory)}")
        + (environment is null ? string.Empty : $", \"environmentVariables\": {{ {environment} }}")
        + " }";

    private void WriteSettings(params string[] profiles)
    {
        File.WriteAllText(ToolPath, string.Empty);
        Directory.CreateDirectory(Path.Combine(_root, "Properties"));
        File.WriteAllText(Path.Combine(_root, "Properties", "launchSettings.json"), "{ \"profiles\": { " + string.Join(", ", profiles) + " } }");
    }

    private void AssertNothingRan()
    {
        Assert.Empty(_host.Processes.Started);
        Assert.Empty(_host.RunProjects.Builds);
        Assert.Empty(_host.Output);
    }

    [Fact]
    public async Task AnUnknownProfileExitsTwo()
    {
        WriteSettings(ToolProfile("Tool"));

        int exitCode = await _host.RunAsync("run", "--launch-profile", "Missing");

        _host.AssertExit(ExitCodes.Usage, exitCode);
        Assert.Contains("error NPR004: No launch profile is named 'Missing'.", _host.Error.ToString(), StringComparison.Ordinal);
        AssertNothingRan();
    }

    [Fact]
    public async Task TheDefaultProfileRunsAfterTheBuild()
    {
        WriteSettings(ToolProfile("Tool", args: "--x 1", environment: "\"V\": \"1\""));

        int exitCode = await _host.RunAsync("run");

        _host.AssertExit(ExitCodes.Success, exitCode);
        ProcessStartRequest started = Assert.Single(_host.Processes.Started);
        Assert.Equal(ToolPath, started.FileName);
        Assert.Equal(["--x", "1"], started.Arguments);
        Assert.Equal(_root, started.WorkingDirectory);
        Assert.Equal("1", started.EnvironmentVariables?["V"]);
        Assert.Equal("Tool", started.EnvironmentVariables?["DOTNET_LAUNCH_PROFILE"]);
        Assert.Single(_host.RunProjects.Builds);
        Assert.Contains("Build succeeded.", _host.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheLaunchProfileOptionSelectsTheProfile()
    {
        WriteSettings(ToolProfile("First", args: "--first"), ToolProfile("Second", args: "--second"));

        _host.AssertExit(ExitCodes.Success, await _host.RunAsync("run", "--launch-profile", "second"));

        ProcessStartRequest started = Assert.Single(_host.Processes.Started);
        Assert.Equal(["--second"], started.Arguments);
        Assert.Equal("Second", started.EnvironmentVariables?["DOTNET_LAUNCH_PROFILE"]);
    }

    [Fact]
    public async Task NoLaunchProfileRunsTheProjectWithNoProfile()
    {
        WriteSettings(ToolProfile("Tool"));

        _host.AssertExit(ExitCodes.Success, await _host.RunAsync("run", "--no-launch-profile"));

        Assert.Equal(NoProfileArguments, Assert.Single(_host.Processes.Started).Arguments);
    }

    [Fact]
    public async Task TheTwoProfileOptionsTogetherExitTwo()
    {
        WriteSettings(ToolProfile("Tool"));

        int exitCode = await _host.RunAsync("run", "--launch-profile", "Tool", "--no-launch-profile");

        _host.AssertExit(ExitCodes.Usage, exitCode);
        Assert.Contains("--launch-profile and --no-launch-profile cannot be used together.", _host.Error.ToString() + _host.Output, StringComparison.Ordinal);
        Assert.Empty(_host.Processes.Started);
        Assert.Empty(_host.RunProjects.Builds);
    }

    [Fact]
    public async Task ForwardedArgumentsReplaceTheProfilesArgumentsAndKeepItsEnvironmentAndDirectory()
    {
        WriteSettings(ToolProfile("Tool", args: "--old", workingDirectory: "sub", environment: "\"V\": \"1\""));

        _host.AssertExit(ExitCodes.Success, await _host.RunAsync("run", "--", "new", "two words"));

        ProcessStartRequest started = Assert.Single(_host.Processes.Started);
        Assert.Equal(["new", "two words"], started.Arguments);
        Assert.Equal(Path.Combine(_root, "sub"), started.WorkingDirectory);
        Assert.Equal("1", started.EnvironmentVariables?["V"]);
    }

    [Fact]
    public async Task TheConfigurationOptionReachesTheBuildAndTheProgramArguments()
    {
        _host.AssertExit(ExitCodes.Success, await _host.RunAsync("run", "-c", "Release"));

        Assert.Equal([(_project, "Release")], _host.RunProjects.Builds);
        string[] arguments = [.. Assert.Single(_host.Processes.Started).Arguments];
        Assert.Equal("Release", arguments[Array.IndexOf(arguments, "-c") + 1]);
    }

    public static TheoryData<string, int> BlockedCases => new()
    {
        { "Npr001InvalidJson", ExitCodes.Failed },
        { "Npr002NamesDifferByCase", ExitCodes.Failed },
        { "Npr003Kind", ExitCodes.Usage },
        { "Npr004UnknownProfile", ExitCodes.Usage },
        { "Npr005PathVariableUnset", ExitCodes.Failed },
        { "Npr006PathProperty", ExitCodes.Failed },
        { "Npr008NotFound", ExitCodes.Failed },
        { "Npr008Empty", ExitCodes.Failed },
        { "Npr009Cmd", ExitCodes.Failed },
        { "Npr010NoBundleExecutable", ExitCodes.Failed },
        { "Npr011ProjectOnLibrary", ExitCodes.Usage },
    };

    [Theory]
    [MemberData(nameof(BlockedCases))]
    public async Task ABlockedRunExitsWithTheContractsCodeAndNamesTheProblemOnStderrOnly(string name, int expectedExit)
    {
        RunProfileCase blocked = RunProfileCases.Named(name).Materialize(Directory.CreateTempSubdirectory("np-run-case-").FullName);
        try
        {
            CliTestHost host = HostFor(blocked);

            int exitCode = await host.RunAsync(ArgumentsFor(blocked));

            host.AssertExit(expectedExit, exitCode);
            string error = host.Error.ToString();
            foreach (string code in blocked.ExpectedProblems)
            {
                Assert.Contains($"error {code}:", error, StringComparison.Ordinal);
            }

            Assert.Empty(host.Output);
            Assert.Empty(host.Processes.Started);
            Assert.Empty(host.RunProjects.Builds);
        }
        finally
        {
            Directory.Delete(blocked.Root ?? string.Empty, recursive: true);
        }
    }

    public static TheoryData<string> Cases => new(RunProfileCases.Names);

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task EveryRunProfileCaseHandsTheExpectedRequestToTheRunner(string name)
    {
        RunProfileCase materialized = RunProfileCases.Named(name).Materialize(Directory.CreateTempSubdirectory("np-run-case-").FullName);
        try
        {
            Assert.SkipWhen(materialized.SkipReason is not null, materialized.SkipReason ?? string.Empty);
            CliTestHost host = HostFor(materialized);

            int exitCode = await host.RunAsync(ArgumentsFor(materialized));

            string error = host.Error.ToString();
            if (materialized.ExpectedRequest is not { } expected)
            {
                Assert.NotEqual(ExitCodes.Success, exitCode);
                Assert.Empty(host.Processes.Started);
            }
            else
            {
                host.AssertExit(ExitCodes.Success, exitCode);
                RunRequestAssert.Same(expected, Assert.Single(host.Processes.Started));
            }

            Assert.Equal(materialized.ExpectedProblems, ProblemCodes(error));
        }
        finally
        {
            Directory.Delete(materialized.Root ?? string.Empty, recursive: true);
        }
    }

    [Fact]
    public async Task AProgramThatCannotBeStartedIsAProblemLineAndExitOneNotAnException()
    {
        _host.Processes.ThrowOnRun = new System.ComponentModel.Win32Exception("No such file or directory");

        int exitCode = await _host.RunAsync("run");

        _host.AssertExit(ExitCodes.Failed, exitCode);
        string error = _host.Error.ToString();
        Assert.Contains("could not be started: No such file or directory", error, StringComparison.Ordinal);
        Assert.Contains("error NPR008:", error, StringComparison.Ordinal);
        Assert.DoesNotContain("Internal error", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARealProfileProgramThatCannotStartExitsOneNotFour()
    {
        var sample = new SampleCopy();
        try
        {
            string notProgram = sample.Combine("not-a-program.txt");
            await File.WriteAllTextAsync(notProgram, "plain text", TestContext.Current.CancellationToken);
            Directory.CreateDirectory(sample.Combine("Properties"));
            await File.WriteAllTextAsync(
                sample.Combine("Properties/launchSettings.json"),
                "{ \"profiles\": { \"Broken\": { \"commandName\": \"Executable\", \"executablePath\": " + Quote(notProgram) + " } } }",
                TestContext.Current.CancellationToken);
            var host = new CliTestHost(sample.Directory);

            int exitCode = await host.RunRealAsync("run", sample.Project);

            host.AssertExit(ExitCodes.Failed, exitCode);
            Assert.Contains("could not be started", host.Error.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("Internal error", host.Error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            sample.Dispose();
        }
    }

    [Fact]
    public void TheExitCodeIsDecidedByTheFirstError()
    {
        RunProblem warning = new(NprDiagnosticCodes.ArgumentVariableUnset, RunProblemSeverity.Warning, "w");
        RunProblem usage = new(NprDiagnosticCodes.UnknownProfile, RunProblemSeverity.Error, "u");
        RunProblem failure = new(NprDiagnosticCodes.ExecutableNotFound, RunProblemSeverity.Error, "f");

        Assert.Equal(ExitCodes.Success, RunExitCodes.For([]));
        Assert.Equal(ExitCodes.Success, RunExitCodes.For([warning]));
        Assert.Equal(ExitCodes.Usage, RunExitCodes.For([warning, usage, failure]));
        Assert.Equal(ExitCodes.Failed, RunExitCodes.For([failure, usage]));
        Assert.Equal(ExitCodes.Usage, RunExitCodes.For([new(NprDiagnosticCodes.KindNotRunnable, RunProblemSeverity.Error, "k")]));
        Assert.Equal(ExitCodes.Usage, RunExitCodes.For([new(NprDiagnosticCodes.ProjectKindNeedsExecutable, RunProblemSeverity.Error, "p")]));
    }

    [Fact]
    public async Task RunHelpNamesTheProfileRulesAndTheExamples()
    {
        Assert.Equal(ExitCodes.Success, await _host.RunAsync("run", "--help"));

        Assert.Contains("netprints run --launch-profile \"Godot editor\"", _host.Output, StringComparison.Ordinal);
        Assert.Contains("netprints run -c Release -- --verbose", _host.Output, StringComparison.Ordinal);
        Assert.Contains("launchSettings.json", _host.Output, StringComparison.Ordinal);
        Assert.Contains("--no-launch-profile", _host.Output, StringComparison.Ordinal);
    }

    private static CliTestHost HostFor(RunProfileCase materialized)
    {
        var host = new CliTestHost(materialized.Root);
        host.RunProjects = FakeRunProjectSystem.For(materialized);
        host.Planner = materialized.CreatePlanner(host.RunProjects);
        return host;
    }

    private static string[] ArgumentsFor(RunProfileCase materialized)
    {
        var arguments = new List<string> { "run", materialized.ProjectFilePath };
        if (materialized.Configuration != RunConfigurations.Debug)
        {
            arguments.AddRange(["-c", materialized.Configuration]);
        }

        if (materialized.NoLaunchProfile)
        {
            arguments.Add("--no-launch-profile");
        }

        if (materialized.ProfileName is not null)
        {
            arguments.AddRange(["--launch-profile", materialized.ProfileName]);
        }

        if (materialized.ArgumentOverride is not null)
        {
            arguments.Add("--");
            arguments.AddRange(materialized.ArgumentOverride);
        }

        return [.. arguments];
    }

    private static List<string> ProblemCodes(string stderr) =>
        [.. stderr.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => line.StartsWith("error NPR", StringComparison.Ordinal) || line.StartsWith("warning NPR", StringComparison.Ordinal))
            .Select(line => line.Split(' ', 3)[1].TrimEnd(':'))];
}
