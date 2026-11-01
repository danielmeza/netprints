using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NetPrints.Core;
using NetPrints.Projects;
using NetPrints.Run.Testing;
using NetPrints.Workspace;
using NetPrints.Workspace.Run;
using Xunit;

namespace NetPrints.Tests.Run;

/// <summary>FR-054 to FR-057, run-profiles.md §2 and §7.3a: the resolver over every <see cref="RunProfileCases"/> case and its edges.</summary>
public sealed class RunProfileResolverTests : IDisposable
{
    private readonly List<string> _roots = [];

    public static TheoryData<string> Cases => new(RunProfileCases.Names);

    public void Dispose()
    {
        foreach (string root in _roots)
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void AnUnsetVariableInTheExecutablePathBlocksBeforeTheBuild()
    {
        RunProfileCase materialized = Materialize("Npr005PathVariableUnset");

        RunResolution resolution = RunProfileResolver.Resolve(materialized.ToInputs(), materialized.Platform);

        Assert.True(resolution.IsBlocked);
        RunProblem problem = Assert.Single(resolution.Problems);
        Assert.Equal(NprDiagnosticCodes.PathVariableUnset, problem.Code);
        Assert.Contains("GODOT_UNSET", problem.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void EveryCaseResolvesAsItsExpectation(string name)
    {
        RunProfileCase materialized = Materialize(name);
        Assert.SkipWhen(materialized.SkipReason is not null, materialized.SkipReason ?? string.Empty);

        RunResolution resolution = RunProfileResolver.Resolve(materialized.ToInputs(), materialized.Platform);

        Assert.Equal(materialized.ExpectedProblems, resolution.Problems.Select(problem => problem.Code));
        Assert.Equal(materialized.ExpectedRequest is null, resolution.IsBlocked);
        if (materialized.ExpectedRequest is { } expected)
        {
            RunRequestAssert.Same(expected, Assert.IsType<ProcessStartRequest>(resolution.Request));
        }
    }

    [Fact]
    public void ABlockedChoiceHoldsOnlyItsProblems()
    {
        RunProfileCase materialized = Materialize("Npr004UnknownProfile");

        RunResolution resolution = RunProfileResolver.Resolve(materialized.ToInputs(), materialized.Platform);

        Assert.Null(resolution.Request);
        Assert.Equal([NprDiagnosticCodes.UnknownProfile], resolution.Problems.Select(problem => problem.Code));
        Assert.Empty(resolution.AddedEnvironment);
    }

    [Fact]
    public void ExpandedTextIsNotExpandedAgainAndALoneBlockStays()
    {
        RunProfileCase materialized = Materialize("ExpandEnvironment") with
        {
            Environment = new Dictionary<string, string> { ["HOME_DIR"] = "%OTHER%", ["OTHER"] = "no" },
        };

        RunResolution resolution = RunProfileResolver.Resolve(materialized.ToInputs(), materialized.Platform);

        ProcessStartRequest request = Assert.IsType<ProcessStartRequest>(resolution.Request);
        Assert.Equal(["--home", "%OTHER%", "--x"], request.Arguments);
    }

    [Fact]
    public void ALonePercentSignStaysLiteral()
    {
        RunProfileCase materialized = Materialize("ExpandEnvironment");
        string tool = (materialized.Root ?? string.Empty).Replace("\\", "/", StringComparison.Ordinal) + "/bin/tool";
        LaunchSettings settings = LaunchSettingsReader.Parse(
            "{\"profiles\":{\"Tool\":{\"commandName\":\"Executable\",\"executablePath\":\"" + tool + "\",\"commandLineArgs\":\"100% done\"}}}",
            Path.Combine(materialized.Root ?? string.Empty, "launchSettings.json"));
        RunInputs inputs = materialized.ToInputs() with { LaunchSettings = settings };

        RunResolution resolution = RunProfileResolver.Resolve(inputs, RunPlatform.Linux);

        Assert.Empty(resolution.Problems);
        Assert.Equal(["100%", "done"], Assert.IsType<ProcessStartRequest>(resolution.Request).Arguments);
    }

    [Fact]
    public void EnvironmentNamesCompareCaseInsensitivelyOnWindowsOnly()
    {
        RunProfileCase windows = Materialize("ExpandEnvironment") with { Platform = RunPlatform.Windows, Environment = new Dictionary<string, string> { ["home_dir"] = "w" } };
        RunProfileCase linux = windows with { Platform = RunPlatform.Linux };

        RunResolution onWindows = RunProfileResolver.Resolve(windows.ToInputs(), RunPlatform.Windows);
        RunResolution onLinux = RunProfileResolver.Resolve(linux.ToInputs(), RunPlatform.Linux);

        Assert.Equal(["--home", "w", "--x"], Assert.IsType<ProcessStartRequest>(onWindows.Request).Arguments);
        Assert.Contains(onLinux.Problems, problem => problem.Code == NprDiagnosticCodes.ArgumentVariableUnset);
    }

    [Fact]
    public void ADefinedEmptyPropertyExpandsToTheEmptyString()
    {
        RunProfileCase materialized = Materialize("ExpandProperty") with { Properties = new Dictionary<string, string> { ["OutDir"] = string.Empty, ["assemblyname"] = "App" } };

        RunResolution resolution = RunProfileResolver.Resolve(materialized.ToInputs(), materialized.Platform);

        Assert.Empty(resolution.Problems);
        Assert.Equal(["--out", "--name", "App"], Assert.IsType<ProcessStartRequest>(resolution.Request).Arguments);
    }

    [Fact]
    public void AMissingPlistIsNotAnException()
    {
        RunProfileCase materialized = Materialize("Npr010NoBundleExecutable");
        File.Delete(Path.Combine(materialized.Root ?? string.Empty, "Bad.app", "Contents", "Info.plist"));

        RunResolution resolution = RunProfileResolver.Resolve(materialized.ToInputs(), RunPlatform.MacOS);

        Assert.Equal([NprDiagnosticCodes.AppBundleUnresolved], resolution.Problems.Select(problem => problem.Code));
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("..")]
    [InlineData("")]
    public void ABundleExecutableThatLeavesTheBundleIsRefused(string executable)
    {
        string bundle = NewRoot();
        Directory.CreateDirectory(Path.Combine(bundle, "Contents"));
        File.WriteAllText(
            Path.Combine(bundle, "Contents", "Info.plist"),
            $"<plist><dict><key>CFBundleExecutable</key><string>{executable}</string></dict></plist>");

        Assert.Null(RunPlatform.MacOS.BundleExecutable(bundle));
    }

    [Fact]
    public void ATargetWithAnInvalidCharacterIsAProblemNotAnException()
    {
        RunProfileCase materialized = Materialize("Npr008NotFound");
        LaunchSettings settings = LaunchSettingsReader.Parse(
            """{"profiles":{"Tool":{"commandName":"Executable","executablePath":"bad\u0000name/tool","workingDirectory":"w\u0000d"}}}""",
            Path.Combine(materialized.Root ?? string.Empty, "launchSettings.json"));

        RunResolution resolution = RunProfileResolver.Resolve(materialized.ToInputs() with { LaunchSettings = settings }, RunPlatform.Linux);

        Assert.True(resolution.IsBlocked);
        Assert.Contains(resolution.Problems, problem => problem.Code == NprDiagnosticCodes.ExecutableNotFound);
    }

    [Fact]
    public void ChooseLetsTheFileProblemsBlockBeforeAnyOtherRule()
    {
        LaunchSettings settings = LaunchSettingsReader.Parse("{ nope", "launchSettings.json");

        RunProfileChoice choice = RunProfileResolver.Choose(settings, BinaryType.Executable, "Anything", noLaunchProfile: true);

        Assert.True(choice.IsBlocked);
        Assert.Equal(NprDiagnosticCodes.LaunchSettingsInvalid, Assert.Single(choice.Problems).Code);
    }

    [Fact]
    public void ChooseGivesNoProfileWhenNoneCanRunAndWhenAskedForNone()
    {
        LaunchSettings onlyProject = LaunchSettingsReader.Parse("""{"profiles":{"Run":{"commandName":"Project"}}}""", "launchSettings.json");

        RunProfileChoice onLibrary = RunProfileResolver.Choose(onlyProject, BinaryType.SharedLibrary, null, noLaunchProfile: false);
        RunProfileChoice declined = RunProfileResolver.Choose(onlyProject, BinaryType.Executable, "Run", noLaunchProfile: true);
        RunProfileChoice chosen = RunProfileResolver.Choose(onlyProject, BinaryType.Executable, "RUN", noLaunchProfile: false);

        Assert.Null(onLibrary.Profile);
        Assert.False(onLibrary.IsBlocked);
        Assert.Null(declined.Profile);
        Assert.Equal("Run", chosen.Profile?.Name);
    }

    [Fact]
    public void PropertyNamesAreDistinctInFieldOrderAndKeepTheFirstSpelling()
    {
        LaunchSettings settings = LaunchSettingsReader.Parse(
            """{"profiles":{"P":{"commandName":"Executable","executablePath":"$(Tool)/x","commandLineArgs":"$(OutDir) $(tool) $(1bad)","workingDirectory":"$(Wd)","environmentVariables":{"A":"$(EnvProp)-$(OUTDIR)"}}}}""",
            "launchSettings.json");

        IReadOnlyList<string> names = RunProfileResolver.PropertyNames(Assert.Single(settings.Profiles));

        Assert.Equal(["Tool", "OutDir", "Wd", "EnvProp"], names);
    }

    [Fact]
    public void ThePlatformsDifferWhereTheContractSaysTheyDo()
    {
        Assert.Equal(';', RunPlatform.Windows.PathListSeparator);
        Assert.Equal(':', RunPlatform.Linux.PathListSeparator);
        Assert.True(RunPlatform.Windows.HasDirectorySeparator("a\\b"));
        Assert.False(RunPlatform.Linux.HasDirectorySeparator("a\\b"));
        Assert.Equal(["tool.exe", "tool.com"], RunPlatform.Windows.CandidateNames("tool"));
        Assert.Equal(["tool.bat", "tool.bat.exe", "tool.bat.com"], RunPlatform.Windows.CandidateNames("tool.bat"));
        Assert.Equal(["tool"], RunPlatform.Linux.CandidateNames("tool"));
        Assert.True(RunPlatform.Windows.RefusesTarget("x/Run.BAT"));
        Assert.False(RunPlatform.Linux.RefusesTarget("x/run.cmd"));
        Assert.True(RunPlatform.Windows.VariableNameComparer.Equals("Path", "PATH"));
        Assert.False(RunPlatform.MacOS.VariableNameComparer.Equals("Path", "PATH"));
    }

    [Fact]
    public void EveryCaseNameIsUniqueAndTheEditorSubsetOmitsTheCliOnlyOnes()
    {
        Assert.Equal(RunProfileCases.Names.Count, RunProfileCases.Names.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            ["NoLaunchProfileOption", "ArgumentOverride", "Npr004UnknownProfile"],
            RunProfileCases.Names.Except(RunProfileCases.EditorNames));
    }

    private RunProfileCase Materialize(string name) => RunProfileCases.Named(name).Materialize(NewRoot());

    private string NewRoot()
    {
        string root = Directory.CreateTempSubdirectory("np-resolve-").FullName;
        _roots.Add(root);
        return root;
    }
}
