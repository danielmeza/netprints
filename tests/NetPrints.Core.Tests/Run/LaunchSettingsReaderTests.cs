using System;
using System.IO;
using System.Linq;
using NetPrints.Core;
using NetPrints.Workspace.Run;
using Xunit;

namespace NetPrints.Tests.Run;

/// <summary>FR-051, FR-052, run-profiles.md §1 and §7.1: reading <c>Properties/launchSettings.json</c> and deciding which profiles can run.</summary>
public class LaunchSettingsReaderTests
{
    private static string Case(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "RunProfiles", "Reader", name);

    private static LaunchSettings ReadCase(string name) => LaunchSettingsReader.Read(Case(name));

    private static LaunchProfile Profile(LaunchSettings settings, string name)
    {
        LaunchProfile? profile = settings.Find(name);
        Assert.NotNull(profile);
        return profile;
    }

    [Fact]
    public void AProjectProfileOnALibraryIsDisabledWithNpr011()
    {
        LaunchSettings settings = ReadCase("library-project");

        RunProfileAvailability availability = Profile(settings, "Game").AvailabilityFor(BinaryType.SharedLibrary);

        Assert.False(availability.CanRun);
        Assert.NotNull(availability.Reason);
        Assert.Equal("NPR011", availability.Reason.Code);
        Assert.Equal(RunProblemSeverity.Error, availability.Reason.Severity);
        Assert.Equal("The Project kind runs only an executable project; this project's output type is SharedLibrary.", availability.Reason.Message);
    }

    [Fact]
    public void AProjectProfileOnAnExecutableRuns()
    {
        Assert.True(Profile(ReadCase("library-project"), "Game").AvailabilityFor(BinaryType.Executable).CanRun);
    }

    [Fact]
    public void AnExecutableProfileRunsOnAnyOutputType()
    {
        LaunchProfile tool = Profile(ReadCase("library-executable"), "Tool");

        Assert.True(tool.AvailabilityFor(BinaryType.SharedLibrary).CanRun);
        Assert.True(tool.AvailabilityFor(BinaryType.Executable).CanRun);
    }

    [Fact]
    public void CommentsAndTrailingCommasAreAllowedAndFileOrderIsKept()
    {
        LaunchSettings settings = ReadCase("basic");

        Assert.True(settings.IsUsable);
        Assert.Empty(settings.Problems);
        Assert.True(settings.HasComments);
        Assert.Equal(["Zeta", "Alpha"], settings.Profiles.Select(profile => profile.Name));
        Assert.Equal(Path.Combine(Case("basic"), "Properties", "launchSettings.json"), settings.FilePath);
    }

    [Fact]
    public void ProfileMembersAreReadAsWritten()
    {
        LaunchSettings settings = ReadCase("basic");

        LaunchProfile zeta = settings.Profiles[0];
        Assert.Equal("Project", zeta.CommandName);
        Assert.Equal("--fast \"a b\"", zeta.CommandLineArgs);
        Assert.Equal("bin", zeta.WorkingDirectory);
        Assert.Null(zeta.ExecutablePath);
        Assert.Equal(
            [new("B_VAR", "2"), new("A_VAR", "1")],
            zeta.EnvironmentVariables);

        LaunchProfile alpha = settings.Profiles[1];
        Assert.Equal("Executable", alpha.CommandName);
        Assert.Equal("%GODOT4%", alpha.ExecutablePath);
        Assert.Empty(alpha.EnvironmentVariables);
    }

    [Fact]
    public void AFileWithoutCommentsHasNoCommentFlag()
    {
        Assert.False(ReadCase("library-project").HasComments);
    }

    [Fact]
    public void ACommentInsideAStringIsNotAComment()
    {
        LaunchSettings settings = LaunchSettingsReader.Parse("""{ "profiles": { "A": { "commandName": "Project", "commandLineArgs": "// not a comment" } } }""", "x.json");

        Assert.False(settings.HasComments);
        Assert.Equal("// not a comment", settings.Profiles[0].CommandLineArgs);
    }

    [Fact]
    public void ABlockCommentSetsTheCommentFlag()
    {
        LaunchSettings settings = LaunchSettingsReader.Parse("""{ /* c */ "profiles": { "A": { "commandName": "Project" } } }""", "x.json");

        Assert.True(settings.HasComments);
        Assert.True(settings.IsUsable);
    }

    [Fact]
    public void CommandNameIsCaseSensitive()
    {
        LaunchSettings settings = ReadCase("kinds");

        RunProfileAvailability lower = Profile(settings, "Lower").AvailabilityFor(BinaryType.Executable);

        Assert.False(lower.CanRun);
        Assert.Equal("NPR003", lower.Reason?.Code);
        Assert.Equal("NetPrints cannot run project profiles.", lower.Reason?.Message);
    }

    [Theory]
    [InlineData("Web", "IISExpress")]
    [InlineData("Box", "Docker")]
    public void OtherKindsAreDisabledWithNpr003(string name, string kind)
    {
        RunProfileAvailability availability = Profile(ReadCase("kinds"), name).AvailabilityFor(BinaryType.Executable);

        Assert.False(availability.CanRun);
        Assert.Equal("NPR003", availability.Reason?.Code);
        Assert.Equal($"NetPrints cannot run {kind} profiles.", availability.Reason?.Message);
    }

    [Fact]
    public void ProjectAndExecutableRun()
    {
        LaunchSettings settings = ReadCase("kinds");

        Assert.True(Profile(settings, "Tool").AvailabilityFor(BinaryType.SharedLibrary).CanRun);
        Assert.True(Profile(settings, "Game").AvailabilityFor(BinaryType.Executable).CanRun);
        Assert.Null(RunProfileAvailability.Runnable.Reason);
    }

    [Fact]
    public void NamesAreComparedCaseInsensitively()
    {
        LaunchSettings settings = ReadCase("basic");

        Assert.Same(settings.Profiles[0], settings.Find("zETA"));
        Assert.Null(settings.Find("Missing"));
    }

    [Fact]
    public void NamesThatDifferOnlyByCaseGiveNpr002AndKeepBothProfiles()
    {
        LaunchSettings settings = ReadCase("case-collision");

        Assert.False(settings.IsUsable);
        RunProblem problem = Assert.Single(settings.Problems);
        Assert.Equal("NPR002", problem.Code);
        Assert.Equal(RunProblemSeverity.Error, problem.Severity);
        Assert.Equal($"The profile names 'Game' and 'game' in '{settings.FilePath}' differ only by case.", problem.Message);
        Assert.Equal(["Game", "Other", "game"], settings.Profiles.Select(profile => profile.Name));
        Assert.Null(settings.DefaultProfile(BinaryType.Executable));
    }

    [Theory]
    [InlineData("invalid-json", null)]
    [InlineData("root-array", "the root is not an object")]
    [InlineData("profiles-array", "'profiles' is not an object")]
    [InlineData("profile-not-object", "profile 'Game' is not an object")]
    [InlineData("no-command", "profile 'Game' has no string commandName")]
    [InlineData("command-not-string", "profile 'Game' has no string commandName")]
    [InlineData("args-number", "'commandLineArgs' of profile 'Game' is not a string")]
    [InlineData("path-number", "'executablePath' of profile 'Game' is not a string")]
    [InlineData("dir-number", "'workingDirectory' of profile 'Game' is not a string")]
    [InlineData("env-not-object", "'environmentVariables' of profile 'Game' is not an object of strings")]
    [InlineData("env-value-number", "'environmentVariables' of profile 'Game' is not an object of strings")]
    public void ABadFileGivesNpr001AndNoProfiles(string name, string? detail)
    {
        LaunchSettings settings = ReadCase(name);

        Assert.False(settings.IsUsable);
        Assert.Empty(settings.Profiles);
        RunProblem problem = Assert.Single(settings.Problems);
        Assert.Equal("NPR001", problem.Code);
        Assert.Equal(RunProblemSeverity.Error, problem.Severity);
        Assert.StartsWith($"'{settings.FilePath}' is not valid launch settings: ", problem.Message, StringComparison.Ordinal);
        Assert.EndsWith(".", problem.Message, StringComparison.Ordinal);
        if (detail is not null)
        {
            Assert.EndsWith($": {detail}.", problem.Message, StringComparison.Ordinal);
        }

        Assert.Null(settings.DefaultProfile(BinaryType.Executable));
    }

    [Theory]
    [InlineData("""{ "profiles": { "Bad\ud800": { "commandName": "Project" } } }""")]
    [InlineData("""{ "profiles": { "Game": { "commandName": "Pro\ud800" } } }""")]
    [InlineData("""{ "profiles": { "Game": { "commandName": "Executable", "executablePath": "a\ud800" } } }""")]
    [InlineData("""{ "profiles": { "Game": { "commandName": "Project", "environmentVariables": { "K\ud800": "v" } } } }""")]
    [InlineData("""{ "profiles": { "Game": { "commandName": "Project", "environmentVariables": { "K": "v\ud800" } } } }""")]
    public void ALoneSurrogateEscapeGivesNpr001(string json)
    {
        LaunchSettings settings = LaunchSettingsReader.Parse(json, "x.json");

        Assert.False(settings.IsUsable);
        Assert.Empty(settings.Profiles);
        RunProblem problem = Assert.Single(settings.Problems);
        Assert.Equal("NPR001", problem.Code);
        Assert.Equal(RunProblemSeverity.Error, problem.Severity);
        Assert.StartsWith("'x.json' is not valid launch settings: ", problem.Message, StringComparison.Ordinal);
        Assert.EndsWith(".", problem.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("..", problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NestingBeyondTheLimitGivesNpr001()
    {
        string json = new string('[', 100) + new string(']', 100);

        LaunchSettings settings = LaunchSettingsReader.Parse(json, "x.json");

        Assert.Equal("NPR001", Assert.Single(settings.Problems).Code);
    }

    [Fact]
    public void InvalidUtf8BytesOnDiskGiveNpr001OrNoException()
    {
        string folder = Path.Combine(Path.GetTempPath(), "nprints-reader-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(folder, "Properties"));
        try
        {
            byte[] head = System.Text.Encoding.UTF8.GetBytes("""{ "profiles": { "Game": { "commandName": "Project", "commandLineArgs": "a""");
            byte[] tail = System.Text.Encoding.UTF8.GetBytes("""b" } } }""");
            byte[] bytes = [.. head, 0xFF, 0xC3, .. tail];
            File.WriteAllBytes(Path.Combine(folder, "Properties", "launchSettings.json"), bytes);

            LaunchSettings settings = LaunchSettingsReader.Read(folder);

            Assert.NotNull(settings);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void ANpr001FileKeepsNoProfileEvenWhenAnEarlierOneWasValid()
    {
        LaunchSettings settings = LaunchSettingsReader.Parse("""{ "profiles": { "Good": { "commandName": "Project" }, "Bad": 1 } }""", "x.json");

        Assert.Empty(settings.Profiles);
        Assert.Equal("NPR001", Assert.Single(settings.Problems).Code);
    }

    [Fact]
    public void TheDefaultIsTheFirstProfileThatCanRun()
    {
        LaunchSettings settings = ReadCase("kinds");

        Assert.Equal("Tool", settings.DefaultProfile(BinaryType.SharedLibrary)?.Name);
        Assert.Equal("Tool", settings.DefaultProfile(BinaryType.Executable)?.Name);
    }

    [Fact]
    public void TheDefaultSkipsAProjectProfileOnALibrary()
    {
        LaunchSettings settings = ReadCase("library-executable");

        Assert.Equal("Tool", settings.DefaultProfile(BinaryType.SharedLibrary)?.Name);
        Assert.Equal("Game", settings.DefaultProfile(BinaryType.Executable)?.Name);
    }

    [Fact]
    public void ThereIsNoDefaultOnALibraryWhoseOnlyProfileIsProject()
    {
        Assert.Null(ReadCase("library-project").DefaultProfile(BinaryType.SharedLibrary));
    }

    [Fact]
    public void ThereIsNoDefaultWhenNoKindCanRun()
    {
        LaunchSettings settings = ReadCase("none-runnable");

        Assert.True(settings.IsUsable);
        Assert.Single(settings.Profiles);
        Assert.Null(settings.DefaultProfile(BinaryType.Executable));
    }

    [Theory]
    [InlineData("no-profiles-member")]
    [InlineData("empty-profiles")]
    public void AFileWithNoProfilesHasNoProblem(string name)
    {
        LaunchSettings settings = ReadCase(name);

        Assert.True(settings.IsUsable);
        Assert.Empty(settings.Profiles);
        Assert.NotNull(settings.FilePath);
    }

    [Fact]
    public void UnknownMembersAreIgnored()
    {
        LaunchSettings settings = ReadCase("unknown-members");

        Assert.True(settings.IsUsable);
        Assert.Equal("Game", Assert.Single(settings.Profiles).Name);
    }

    [Fact]
    public void RunJsonAndSpacedFoldersGiveNone()
    {
        LaunchSettings settings = ReadCase("ignored");

        Assert.Same(LaunchSettings.None, settings);
        Assert.Null(settings.FilePath);
        Assert.Empty(settings.Profiles);
        Assert.Empty(settings.Problems);
        Assert.True(settings.IsUsable);
    }

    [Fact]
    public void AMissingFolderGivesNone()
    {
        Assert.Same(LaunchSettings.None, LaunchSettingsReader.Read(Path.Combine(Case("ignored"), "no-such-folder")));
    }

    [Fact]
    public void AnUnreadablePathGivesNpr001WithTheIoMessage()
    {
        string folder = Path.Combine(Path.GetTempPath(), "nprints-reader-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(folder, "Properties", "launchSettings.json"));
        try
        {
            LaunchSettings settings = LaunchSettingsReader.Read(folder);

            RunProblem problem = Assert.Single(settings.Problems);
            Assert.Equal("NPR001", problem.Code);
            Assert.Empty(settings.Profiles);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void ToLineWritesSeverityCodeAndMessage()
    {
        var error = new RunProblem("NPR005", RunProblemSeverity.Error, "The variable GODOT4 is not set (executablePath of profile 'Game').");
        var warning = new RunProblem("NPR007", RunProblemSeverity.Warning, "text");

        Assert.Equal("error NPR005: The variable GODOT4 is not set (executablePath of profile 'Game').", error.ToLine());
        Assert.Equal("warning NPR007: text", warning.ToLine());
    }

    [Fact]
    public void OnlyProjectAndExecutableAreRunnableKinds()
    {
        Assert.True(RunProfileKinds.IsRunnable("Project"));
        Assert.True(RunProfileKinds.IsRunnable("Executable"));
        Assert.False(RunProfileKinds.IsRunnable("project"));
        Assert.False(RunProfileKinds.IsRunnable("executable"));
        Assert.False(RunProfileKinds.IsRunnable("IISExpress"));
        Assert.False(RunProfileKinds.IsRunnable(string.Empty));
    }
}
